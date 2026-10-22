using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Infrastructure.Transactions;
namespace ServiceDesk.Tests;

public class HardeningSqlTests
{
    private static TicketService Tickets(ServiceDeskDbContext db) => new(new SupportTicketRepository(db), new TechnicianAssignmentRepository(db), new TicketStatusHistoryRepository(db), new WorkflowTransaction(db));

    [SqlFact]
    public async Task Competing_transitions_commit_once_and_stale_edits_cannot_restore_old_status()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var first = fixture.OpenContext();
        var ticket = await fixture.AddTicketAsync(first, TicketStatus.AwaitingApproval);
        await using var second = fixture.OpenContext();
        var stale = await second.SupportTickets.SingleAsync();
        var originalVersion = stale.Version;
        await Tickets(first).ApproveTicketAsync(ticket.Id, 1, "Approved", ticket.Version);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Tickets(second).ApproveTicketAsync(ticket.Id, 1, "Duplicate", originalVersion));
        await using var editing = fixture.OpenContext();
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => Tickets(editing).UpdateTicketDetailsAsync(ticket.Id, "Stale edit", "Old data", TicketPriority.Low, originalVersion));
        await using var verification = fixture.OpenContext();
        Assert.Equal(TicketStatus.Approved, (await verification.SupportTickets.SingleAsync()).Status);
        Assert.Equal("Rollback test", (await verification.SupportTickets.SingleAsync()).Title);
        Assert.Single(await verification.TicketStatusHistories.ToListAsync());
    }

    [SqlFact]
    public async Task Corrections_preserve_history_and_withdraw_restore_the_recorded_assignment()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await fixture.AddTicketAsync(db, TicketStatus.AwaitingApproval);
        ticket = (await db.SupportTickets.SingleAsync());
        var service = Tickets(db);
        async Task Correct()
        {
            var latest = await db.TicketStatusHistories.OrderByDescending(entry => entry.Id).FirstAsync();
            await service.CorrectStatusAsync(ticket.Id, latest.Id, 1, "Corrected human error", ticket.Version);
        }
        await service.RejectTicketAsync(ticket.Id, 1, "Mistake", ticket.Version);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.UpdateTicketDetailsAsync(ticket.Id, "Changed", "Changed", TicketPriority.High, ticket.Version));
        await Correct();
        Assert.Equal(TicketStatus.AwaitingApproval, ticket.Status);
        await service.ApproveTicketAsync(ticket.Id, 1, null, ticket.Version);
        await service.AssignTicketAsync(ticket.Id, 1, 1, DateTime.UtcNow.AddDays(1), "Visit", 1, null, ticket.Version);
        var assignmentId = (await service.GetLatestAssignmentAsync(ticket.Id))!.Id;
        await Correct();
        Assert.Equal(TicketStatus.Approved, ticket.Status);
        Assert.Null(await service.GetLatestAssignmentAsync(ticket.Id));
        Assert.NotNull((await db.TechnicianAssignments.SingleAsync()).WithdrawnAt);
        await Correct();
        Assert.Equal(TicketStatus.Assigned, ticket.Status);
        Assert.Equal(assignmentId, (await service.GetLatestAssignmentAsync(ticket.Id))!.Id);
        await service.StartProgressTicketAsync(ticket.Id, 1, null, ticket.Version);
        await service.ResolveTicketAsync(ticket.Id, 1, null, ticket.Version);
        await service.CloseTicketAsync(ticket.Id, 1, null, ticket.Version);
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.UpdateTicketDetailsAsync(ticket.Id, "Changed", "Changed", TicketPriority.High, ticket.Version));
        await Correct();
        Assert.Equal(TicketStatus.Resolved, ticket.Status);
        await service.UpdateTicketDetailsAsync(ticket.Id, "Corrected title", "Corrected details", TicketPriority.High, ticket.Version);
        var count = await db.TicketStatusHistories.CountAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.CorrectStatusAsync(ticket.Id, 1, 1, " ", ticket.Version));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.CorrectStatusAsync(ticket.Id, 1, 1, "Stale event", ticket.Version));
        Assert.Equal(count, await db.TicketStatusHistories.CountAsync());
        Assert.Equal(4, await db.TicketStatusHistories.CountAsync(entry => entry.ReversesHistoryId != null));
        Assert.All(await db.TicketStatusHistories.Where(entry => entry.ReversesHistoryId != null).ToListAsync(), entry => Assert.Equal("Corrected human error", entry.Reason));
    }

    [SqlFact]
    public async Task Manager_lifecycle_hashes_passwords_changes_session_versions_and_preserves_history()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var hasher = new PasswordHasher<User>();
        var service = new ManagerService(new ManagerRepository(db), hasher);
        var password = Guid.NewGuid().ToString("N")[..8];
        var user = await service.SaveAsync(null, new("Demo", "Manager", " manager@example.test ", "000", fixture.StoreId, password));
        var login = new LoginService(new UserRepository(db), hasher);
        Assert.NotNull(await login.AuthenticateAsync(" MANAGER@example.test ", password));
        Assert.NotEqual(password, user.PasswordHash);
        var originalVersion = user.Version;
        var originalSession = user.SessionVersion;
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.SaveAsync(null, new("Other", "Manager", "MANAGER@example.test", "000", fixture.StoreId, password)));
        await service.SetActiveAsync(user.Id, false, user.Version);
        Assert.True(user.SessionVersion > originalSession);
        Assert.Null(await login.AuthenticateAsync(user.Email, password));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.SetActiveAsync(user.Id, true, originalVersion));
        await service.SetActiveAsync(user.Id, true, user.Version);
        var newPassword = Guid.NewGuid().ToString("N")[..8];
        var beforeReset = user.SessionVersion;
        await service.ResetPasswordAsync(user.Id, newPassword, user.Version);
        Assert.True(user.SessionVersion > beforeReset);
        Assert.Null(await login.AuthenticateAsync(user.Email, password));
        Assert.NotNull(await login.AuthenticateAsync(user.Email, newPassword));
        var store = new Store("OTHER", "Other store", "Address", "City"); db.Stores.Add(store); await db.SaveChangesAsync();
        var beforeMove = user.SessionVersion;
        await service.SaveAsync(user.Id, new("Demo", "Manager", "changed@example.test", "000", store.Id, ExpectedVersion: user.Version));
        Assert.True(user.SessionVersion > beforeMove);
        Assert.Equal(store.Id, user.StoreId);
        Assert.Contains(await service.GetAllAsync(), item => item.Id == user.Id);
        store.SetActive(false); await db.SaveChangesAsync();
        await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(null, new("Other", "Manager", "other@example.test", "000", store.Id, password)));
    }

    [SqlFact]
    public async Task Foreign_keys_reject_orphan_history_and_ticket_creator()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await fixture.AddTicketAsync(db, TicketStatus.AwaitingApproval);
        db.TicketStatusHistories.Add(new TicketStatusHistory(ticket.Id, ticket.Status, TicketStatus.Approved, int.MaxValue, "Invalid actor"));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.SupportTickets.Add(new SupportTicket("Orphan", "Invalid creator", int.MaxValue, fixture.CategoryId, fixture.StoreId, TicketPriority.Low));
        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }
}
