using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Infrastructure.Transactions;

namespace ServiceDesk.Tests;

public class VisitStatusSqlTests
{
    [SqlFact]
    public async Task Declined_visits_are_prioritized_for_admin_and_scoped_for_managers_until_rescheduled()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await fixture.AddTicketAsync(db, TicketStatus.Approved);
        var service = new TicketService(new SupportTicketRepository(db), new TechnicianAssignmentRepository(db),
            new TicketStatusHistoryRepository(db), new WorkflowTransaction(db));
        await service.AssignTicketAsync(ticket.Id, 1, 1, DateTime.UtcNow.AddDays(1), "Visit", 1, null);
        var tracked = (await service.GetTicketByIdAsync(ticket.Id))!;
        var assignment = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await service.ChangeVisitAsync(ticket.Id, 1, "decline", tracked.Version, assignment.Version, reason: "Store is closed then");
        for (var i = 0; i < 9; i++)
            db.SupportTickets.Add(new SupportTicket("Higher priority", "Waiting approval", 1, fixture.CategoryId, fixture.StoreId, TicketPriority.Critical));
        var other = new Store("OTHER", "Other store", "Address", "City");
        db.Add(other); await db.SaveChangesAsync();
        var queries = new TicketQueryRepository(db);
        Assert.Equal("Declined", (await queries.GetVisitStatesAsync([ticket.Id]))[ticket.Id]);
        var dashboard = await queries.DashboardAsync(null);
        Assert.Equal(ticket.Id, dashboard.Attention.First().Id);
        Assert.Contains((await queries.DashboardAsync(fixture.StoreId)).Attention, t => t.Id == ticket.Id);
        Assert.Empty((await queries.DashboardAsync(other.Id)).Attention);
        Assert.Equal(10, dashboard.Summary.Open);
        assignment = (await service.GetLatestAssignmentAsync(ticket.Id))!;
        await service.ChangeVisitAsync(ticket.Id, 1, "reschedule", tracked.Version, assignment.Version, scheduledAt: DateTime.UtcNow.AddDays(2));
        Assert.Equal("AwaitingConfirmation", (await queries.GetVisitStatesAsync([ticket.Id]))[ticket.Id]);
        Assert.DoesNotContain((await queries.DashboardAsync(null)).Attention, t => t.Id == ticket.Id);
        await service.ChangeVisitAsync(ticket.Id, 1, "decline", tracked.Version, (await service.GetLatestAssignmentAsync(ticket.Id))!.Version, reason: "Still unavailable");
        // An older declined assignment must not override the latest active proposal.
        var previous = await db.TechnicianAssignments.SingleAsync();
        previous.Withdraw();
        db.Add(new TechnicianAssignment(ticket.Id, 1, 1, DateTime.UtcNow.AddDays(3), "New proposal"));
        await db.SaveChangesAsync();
        Assert.Equal("AwaitingConfirmation", (await queries.GetVisitStatesAsync([ticket.Id]))[ticket.Id]);
        Assert.DoesNotContain((await queries.DashboardAsync(null)).Attention, t => t.Id == ticket.Id);
    }
}
