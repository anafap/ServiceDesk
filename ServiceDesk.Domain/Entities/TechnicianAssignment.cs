
namespace ServiceDesk.Domain.Entities;

public class TechnicianAssignment : VersionedEntity
{
    // EF restores historical visits without rerunning rules for a new appointment.
    private TechnicianAssignment() { }

    public DateTime? WithdrawnAt { get; private set; }
    public void Withdraw() { ResetConfirmation(); WithdrawnAt = DateTime.UtcNow; }
    public void Restore() { ResetConfirmation(); WithdrawnAt = null; }

    public int Id { get; private set; }

    public int SupportTicketId { get; private set; }

    public int ExternalTechnicianId { get; private set; }

    public int AssignedByUserId { get; private set; }

    public DateTime AssignedAt { get; private set; }

    public DateTime? ScheduledAt { get; private set; }

    public string Notes { get; private set; } = string.Empty;

    public TechnicianAssignment(int supportTicketId, int externalTechnicianId, int assignedByUserId, DateTime? scheduledAt, string notes)
    {
        if (supportTicketId <= 0)
            throw new ArgumentException("SupportTicketId is required.", nameof(supportTicketId));
        if (externalTechnicianId <= 0)
            throw new ArgumentException("externalTechnicianId is required.", nameof(externalTechnicianId));
        if (assignedByUserId <= 0)
            throw new ArgumentException("AssignedByUserId is required.", nameof(assignedByUserId));
        if (scheduledAt.HasValue && scheduledAt.Value < DateTime.UtcNow)
            throw new ArgumentException(
                "Scheduled date cannot be in the past.",
                nameof(scheduledAt));


        SupportTicketId = supportTicketId;
        ExternalTechnicianId = externalTechnicianId;
        AssignedByUserId = assignedByUserId;
        ScheduledAt = scheduledAt;
        AssignedAt = DateTime.UtcNow;
        Notes = notes ?? string.Empty;



    }
    public string VisitState { get; private set; } = "AwaitingConfirmation";
    public string? ContactName { get; private set; }
    public string? ContactPhone { get; private set; }
    public int? ConfirmedByUserId { get; private set; }
    public DateTime? ConfirmedAt { get; private set; }
    public string? DeclineReason { get; private set; }
    public DateTime? RepairStartedAt { get; private set; }
    private readonly List<VisitEvent> _visitEvents = [];
    public IReadOnlyCollection<VisitEvent> VisitEvents => _visitEvents;

    private void RequireNotStarted()
    {
        if (WithdrawnAt is not null || RepairStartedAt is not null)
            throw new ServiceDesk.Domain.Exceptions.StateConflictException("This visit cannot be changed. Refresh ticket details.");
    }

    public void ConfirmVisit(string name, string phone, int actor)
    {
        RequireNotStarted();
        if (VisitState != "AwaitingConfirmation")
            throw new ServiceDesk.Domain.Exceptions.StateConflictException("Ask Admin to propose a new time before confirming again.");
        if (ScheduledAt is null || ScheduledAt <= DateTime.UtcNow)
            throw new ArgumentException("Ask Admin to choose a future visit time.");
        if (string.IsNullOrWhiteSpace(name) || name.Trim().Length > 100
            || string.IsNullOrWhiteSpace(phone) || phone.Trim().Length > 50)
            throw new ArgumentException("A contact name (up to 100 characters) and phone (up to 50 characters) are required.");
        ContactName = name.Trim(); ContactPhone = phone.Trim();
        ConfirmedByUserId = actor; ConfirmedAt = DateTime.UtcNow;
        VisitState = "Confirmed"; DeclineReason = null;
        RecordVisitEvent("Confirmed", actor);
    }

    public void DeclineVisit(string reason, int actor)
    {
        RequireNotStarted();
        if (VisitState != "AwaitingConfirmation")
            throw new ServiceDesk.Domain.Exceptions.StateConflictException("Only an awaiting-confirmation visit can be declined.");
        if (string.IsNullOrWhiteSpace(reason) || reason.Trim().Length > 2000)
            throw new ArgumentException("A decline reason of up to 2,000 characters is required.");
        ResetConfirmation(); VisitState = "Declined"; DeclineReason = reason.Trim();
        RecordVisitEvent("Declined", actor);
    }

    public void Reschedule(DateTime scheduledAt, int actor)
    {
        RequireNotStarted();
        if (scheduledAt <= DateTime.UtcNow) throw new ArgumentException("Choose a future visit time.");
        if (scheduledAt == ScheduledAt)
            throw new ArgumentException("Choose a different visit time.");
        ScheduledAt = scheduledAt; ResetConfirmation();
        RecordVisitEvent("Rescheduled", actor);
    }

    public void StartRepair(int actor)
    {
        RequireNotStarted();
        if (VisitState != "Confirmed" || ConfirmedByUserId is null || string.IsNullOrWhiteSpace(ContactName) || string.IsNullOrWhiteSpace(ContactPhone))
            throw new ServiceDesk.Domain.Exceptions.StateConflictException("The store manager must confirm the visit and contact person before repair starts.");
        RepairStartedAt = DateTime.UtcNow;
        RecordVisitEvent("RepairStarted", actor);
    }

    public void ResetVisit(int actor)
    {
        ResetConfirmation(); RepairStartedAt = null;
        RecordVisitEvent("ConfirmationReset", actor);
    }

    private void ResetConfirmation()
    {
        VisitState = "AwaitingConfirmation"; ContactName = null; ContactPhone = null;
        ConfirmedByUserId = null; ConfirmedAt = null; DeclineReason = null;
    }

    private void RecordVisitEvent(string kind, int actor) =>
        _visitEvents.Add(new VisitEvent(Id, kind, actor, ScheduledAt, ContactName, ContactPhone, DeclineReason));
}
