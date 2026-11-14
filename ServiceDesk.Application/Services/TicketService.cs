using ServiceDesk.Domain.Exceptions;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Application.Services;

public class TicketService(ISupportTicketRepository tickets, ITechnicianAssignmentRepository assignments,
    ITicketStatusHistoryRepository history, IWorkflowTransaction transaction, INotificationPublisher? notifications = null)
{
    private async Task<SupportTicket> LoadAsync(int id, string? expectedVersion)
    {
        var ticket = await tickets.GetByIdAsync(id) ?? throw new KeyNotFoundException($"Ticket with ID {id} was not found.");
        if (expectedVersion is not null) ticket.EnsureVersion(expectedVersion);
        return ticket;
    }

    private Task TransitionAsync(int id, int actor, string? reason, Action<SupportTicket> change, string? expectedVersion) =>
        transaction.ExecuteAsync(async () =>
        {
            var ticket = await LoadAsync(id, expectedVersion);
            var previous = ticket.Status;
            change(ticket);
            await tickets.SaveAsync(ticket);
            var entry = new TicketStatusHistory(id, previous, ticket.Status, actor, reason);
            await history.AddAsync(entry);
            if (notifications is not null) await notifications.PublishAsync(ticket, actor, $"history:{entry.Id}", "Ticket status changed");
        });

    public Task ApproveTicketAsync(int ticketId, int changedByUserId, string? reason, string? expectedVersion = null) =>
        TransitionAsync(ticketId, changedByUserId, reason, ticket => ticket.Approve(), expectedVersion);
    public Task RejectTicketAsync(int ticketId, int changedByUserId, string? reason, string? expectedVersion = null) =>
        TransitionAsync(ticketId, changedByUserId, reason, ticket => ticket.Reject(), expectedVersion);
    private async Task<TechnicianAssignment> LoadAssignmentAsync(int ticketId, string? expectedAssignmentVersion)
    {
        var current = await assignments.GetLatestByTicketIdAsync(ticketId)
            ?? throw new StateConflictException("The current assignment is missing.");
        var assignment = await assignments.GetByIdAsync(current.Id)
            ?? throw new StateConflictException("The current assignment is missing.");
        assignment.EnsureVersion(expectedAssignmentVersion!);
        return assignment;
    }

    public Task ChangeVisitAsync(int ticketId, int actor, string action, string expectedVersion,
        string expectedAssignmentVersion, string? contactName = null, string? contactPhone = null,
        string? reason = null, DateTime? scheduledAt = null) => transaction.ExecuteAsync(async () =>
    {
        var ticket = await LoadAsync(ticketId, expectedVersion);
        var assignment = await LoadAssignmentAsync(ticketId, expectedAssignmentVersion);
        // Serialize visit decisions against ticket corrections and other workflow actions.
        ticket.RecordVisitChange();
        switch (action)
        {
            case "confirm": assignment.ConfirmVisit(contactName!, contactPhone!, actor); break;
            case "decline": assignment.DeclineVisit(reason!, actor); break;
            case "reschedule": assignment.Reschedule(scheduledAt ?? throw new ArgumentException("Choose a future visit time."), actor); break;
            default: throw new ArgumentException("Unknown visit action.");
        }
        await tickets.SaveAsync(ticket);
        await assignments.SaveAsync(assignment);
        if (notifications is not null)
            await notifications.PublishAsync(ticket, actor, $"visit:{assignment.VisitEvents.Last().Id}",
                action == "confirm" ? "Visit confirmed" : action == "decline" ? "Visit time declined" : "Visit time changed");
    });

    public Task StartProgressTicketAsync(int ticketId, int changedByUserId, string? reason,
        string? expectedVersion = null, string? expectedAssignmentVersion = null) => transaction.ExecuteAsync(async () =>
    {
        var ticket = await LoadAsync(ticketId, expectedVersion);
        var assignment = await LoadAssignmentAsync(ticketId, expectedAssignmentVersion);
        assignment.StartRepair(changedByUserId);
        var previous = ticket.Status;
        ticket.StartProgress();
        await tickets.SaveAsync(ticket);
        await assignments.SaveAsync(assignment);
        var entry = new TicketStatusHistory(ticketId, previous, ticket.Status, changedByUserId, reason);
        entry.RecordAssignment(assignment.Id);
        await history.AddAsync(entry);
        if (notifications is not null) await notifications.PublishAsync(ticket, changedByUserId, $"history:{entry.Id}", "Repair started");
    });
    public Task ResolveTicketAsync(int ticketId, int changedByUserId, string? reason, string? expectedVersion = null) =>
        TransitionAsync(ticketId, changedByUserId, reason, ticket => ticket.Resolve(), expectedVersion);
    public Task CloseTicketAsync(int ticketId, int changedByUserId, string? reason, string? expectedVersion = null) =>
        TransitionAsync(ticketId, changedByUserId, reason, ticket => ticket.Close(), expectedVersion);

    public Task AssignTicketAsync(int ticketId, int externaltechnicianId, int assignedByUserId, DateTime scheduledAt,
        string notes, int changedByUserId, string? reason, string? expectedVersion = null) => transaction.ExecuteAsync(async () =>
    {
        var ticket = await LoadAsync(ticketId, expectedVersion);
        var assignment = new TechnicianAssignment(ticketId, externaltechnicianId, assignedByUserId, scheduledAt, notes);
        var previous = ticket.Status;
        ticket.Assign();
        await tickets.SaveAsync(ticket);
        await assignments.AddAsync(assignment);
        var entry = new TicketStatusHistory(ticketId, previous, ticket.Status, changedByUserId, reason);
        entry.RecordAssignment(assignment.Id);
        await history.AddAsync(entry);
        if (notifications is not null) await notifications.PublishAsync(ticket, changedByUserId, $"history:{entry.Id}", "Technician assigned");
    });

    public Task CorrectStatusAsync(int id, int eventId, int actor, string reason, string expectedVersion) => transaction.ExecuteAsync(async () =>
    {
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000) throw new ArgumentException("A correction reason of at most 2,000 characters is required.");
        var ticket = await LoadAsync(id, expectedVersion);
        var latest = (await history.GetByTicketId(id)).LastOrDefault();
        if (latest is null || latest.Id != eventId || latest.NewStatus != ticket.Status || latest.PreviousStatus is null)
            throw new StateConflictException("The status event changed or cannot be corrected. Refresh ticket details.");
        var previous = ticket.Status;
        int? assignmentId = latest.TechnicianAssignmentId;
        if (previous == TicketStatus.Assigned && latest.PreviousStatus == TicketStatus.Approved)
        {
            var active = await assignments.GetLatestByTicketIdAsync(id) ?? throw new StateConflictException("The current assignment is missing.");
            assignmentId = active.Id;
            var tracked = await assignments.GetByIdAsync(active.Id) ?? throw new StateConflictException("The current assignment is missing.");
            tracked.ResetVisit(actor);
            tracked.Withdraw();
            await assignments.SaveAsync(tracked);
        }
        else if (previous == TicketStatus.Approved && latest.PreviousStatus == TicketStatus.Assigned)
        {
            if (assignmentId is null) throw new StateConflictException("The recorded assignment cannot be restored.");
            var assignment = await assignments.GetByIdAsync(assignmentId.Value);
            if (assignment is null || assignment.SupportTicketId != id) throw new StateConflictException("The recorded assignment cannot be restored.");
            assignment.ResetVisit(actor);
            assignment.Restore();
            await assignments.SaveAsync(assignment);
        }
        if (previous == TicketStatus.InProgress && latest.PreviousStatus == TicketStatus.Assigned)
        {
            var active = await assignments.GetLatestByTicketIdAsync(id) ?? throw new StateConflictException("The current assignment is missing.");
            var assignment = await assignments.GetByIdAsync(active.Id) ?? throw new StateConflictException("The current assignment is missing.");
            assignment.ResetVisit(actor);
            await assignments.SaveAsync(assignment);
        }
        // Reversing a correction must not reinstate InProgress without a new confirmation.
        if (previous == TicketStatus.Assigned && latest.PreviousStatus == TicketStatus.InProgress)
            throw new StateConflictException("Confirm the visit again and use Start repair.");
        ticket.CorrectStatus(latest.PreviousStatus.Value);
        await tickets.SaveAsync(ticket);
        var correction = new TicketStatusHistory(id, previous, ticket.Status, actor, reason.Trim());
        correction.RecordCorrection(latest.Id, assignmentId);
        await history.AddAsync(correction);
        if (notifications is not null) await notifications.PublishAsync(ticket, actor, $"history:{correction.Id}", "Ticket status corrected");
    });

    public async Task<SupportTicket> CreateTicketAsync(string title, string description, int createdByUserId, int categoryId, int storeId, TicketPriority priority)
    {
        var ticket = new SupportTicket(title, description, createdByUserId, categoryId, storeId, priority);
        ticket.UpdateDetails(title, description, priority);
        await transaction.ExecuteAsync(async () =>
        {
            await tickets.AddAsync(ticket);
            if (notifications is not null) await notifications.PublishAsync(ticket, createdByUserId, $"ticket:{ticket.Id}", "Ticket created");
        });
        return ticket;
    }
    public async Task UpdateTicketDetailsAsync(int ticketId, string title, string description, TicketPriority priority, string? expectedVersion = null)
    {
        var ticket = await LoadAsync(ticketId, expectedVersion);
        ticket.UpdateDetails(title, description, priority);
        await tickets.SaveAsync(ticket);
    }
    public Task<List<SupportTicket>> GetAllTicketsAsync(int? storeId = null) => tickets.GetAllAsync(storeId);
    public Task<SupportTicket?> GetTicketByIdAsync(int ticketId) => tickets.GetByIdAsync(ticketId);
    public Task<TechnicianAssignment?> GetLatestAssignmentAsync(int ticketId) => assignments.GetLatestByTicketIdAsync(ticketId);
    public Task<List<TicketStatusHistory>> GetHistoryByTicketIdAsync(int ticketId) => history.GetByTicketId(ticketId);
}
