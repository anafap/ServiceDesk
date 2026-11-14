using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Domain.Exceptions;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Infrastructure.Transactions;

namespace ServiceDesk.Tests;

public class VisitSqlTests
{
    private static TicketService Service(ServiceDeskDbContext db, bool notify = false) => new(
        new SupportTicketRepository(db), new TechnicianAssignmentRepository(db), new TicketStatusHistoryRepository(db),
        new WorkflowTransaction(db), notify ? new NotificationRepository(db) : null);

    private static async Task<SupportTicket> Assign(WorkflowTransactionSqlTests.TestDatabase fixture, ServiceDeskDbContext db)
    {
        var ticket = await fixture.AddTicketAsync(db, TicketStatus.Approved);
        await Service(db).AssignTicketAsync(ticket.Id, 1, 1, DateTime.UtcNow.AddDays(1), "Visit", 1, null, ticket.Version);
        return (await db.SupportTickets.SingleAsync());
    }

    [SqlFact]
    public async Task Visit_decisions_persist_and_notify_atomically_without_polluting_status_history()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await Assign(fixture, db);
        var manager = new User("Store", "Manager", "visit.manager@example.test", "123", UserRole.StoreManager, fixture.StoreId);
        db.Add(manager); await db.SaveChangesAsync();
        var service = Service(db, true);
        var visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await service.ChangeVisitAsync(ticket.Id, manager.Id, "decline", ticket.Version, visit.Version, reason: "Closed then");
        visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await service.ChangeVisitAsync(ticket.Id, 1, "reschedule", ticket.Version, visit.Version, scheduledAt: DateTime.UtcNow.AddDays(2));
        visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await service.ChangeVisitAsync(ticket.Id, manager.Id, "confirm", ticket.Version, visit.Version, "Contact", "+973 12345678");
        visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await service.StartProgressTicketAsync(ticket.Id, 1, null, ticket.Version, visit.Version);
        await using var check = fixture.OpenContext();
        Assert.Equal(TicketStatus.InProgress, (await check.SupportTickets.SingleAsync()).Status);
        Assert.Equal(2, await check.TicketStatusHistories.CountAsync());
        Assert.Equal(4, await check.VisitEvents.CountAsync());
        Assert.Equal(4, await check.Notifications.CountAsync());
        Assert.Equal("Contact", (await check.TechnicianAssignments.SingleAsync()).ContactName);
        var latest = await db.TicketStatusHistories.OrderByDescending(h => h.Id).FirstAsync();
        await service.CorrectStatusAsync(ticket.Id, latest.Id, 1, "Started by mistake", ticket.Version);
        visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        Assert.Equal("AwaitingConfirmation", visit.VisitState);
        Assert.Null(visit.RepairStartedAt);
        await Assert.ThrowsAsync<StateConflictException>(() => service.StartProgressTicketAsync(ticket.Id, 1, null, ticket.Version, visit.Version));
        latest = await db.TicketStatusHistories.OrderByDescending(h => h.Id).FirstAsync();
        await Assert.ThrowsAsync<StateConflictException>(() => service.CorrectStatusAsync(ticket.Id, latest.Id, 1, "Undo correction", ticket.Version));
    }

    [SqlFact]
    public async Task Audit_failure_rolls_back_contact_confirmation_ticket_version_and_notifications()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await Assign(fixture, db);
        var service = Service(db, true);
        var visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        var ticketVersion = ticket.Version;
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE [VisitEvents] ADD CONSTRAINT [CK_Test_VisitFailure] CHECK ([Kind] <> 'Confirmed')");
        await Assert.ThrowsAsync<DbUpdateException>(() => service.ChangeVisitAsync(ticket.Id, 1, "confirm", ticket.Version, visit.Version, "Contact", "123"));
        await using var check = fixture.OpenContext();
        Assert.Equal(ticketVersion, (await check.SupportTickets.SingleAsync()).Version);
        Assert.Equal("AwaitingConfirmation", (await check.TechnicianAssignments.SingleAsync()).VisitState);
        Assert.Empty(await check.VisitEvents.ToListAsync());
        Assert.Empty(await check.Notifications.ToListAsync());
    }

    [SqlTheory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Reschedule_and_repair_start_using_same_snapshot_cannot_both_commit(bool rescheduleFirst)
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var setup = fixture.OpenContext();
        var ticket = await Assign(fixture, setup);
        var service = Service(setup);
        var visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await service.ChangeVisitAsync(ticket.Id, 1, "confirm", ticket.Version, visit.Version, "Contact", "123");
        visit = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await using var first = fixture.OpenContext();
        await using var second = fixture.OpenContext();
        // Both request contexts have already loaded the same ticket and assignment versions.
        await first.SupportTickets.LoadAsync(); await first.TechnicianAssignments.Include(a => a.VisitEvents).LoadAsync();
        await second.SupportTickets.LoadAsync(); await second.TechnicianAssignments.Include(a => a.VisitEvents).LoadAsync();
        if (rescheduleFirst)
        {
            await Service(first).ChangeVisitAsync(ticket.Id, 1, "reschedule", ticket.Version, visit.Version, scheduledAt: DateTime.UtcNow.AddDays(3));
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(second).StartProgressTicketAsync(ticket.Id, 1, null, ticket.Version, visit.Version));
        }
        else
        {
            await Service(first).StartProgressTicketAsync(ticket.Id, 1, null, ticket.Version, visit.Version);
            await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => Service(second).ChangeVisitAsync(ticket.Id, 1, "reschedule", ticket.Version, visit.Version, scheduledAt: DateTime.UtcNow.AddDays(3)));
        }
        await using var check = fixture.OpenContext();
        Assert.Equal(rescheduleFirst ? TicketStatus.Assigned : TicketStatus.InProgress, (await check.SupportTickets.SingleAsync()).Status);
        Assert.Equal(rescheduleFirst ? "AwaitingConfirmation" : "Confirmed", (await check.TechnicianAssignments.SingleAsync()).VisitState);
        Assert.Equal(rescheduleFirst ? 0 : 1, await check.VisitEvents.CountAsync(e => e.Kind == "RepairStarted"));
    }
}
