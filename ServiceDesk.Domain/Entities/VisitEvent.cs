namespace ServiceDesk.Domain.Entities;

// Separate append-only visit decisions; never eligible for status reversal.
public sealed class VisitEvent
{
    private VisitEvent() { }
    public VisitEvent(int assignmentId, string kind, int actor, DateTime? scheduledAt,
        string? contactName, string? contactPhone, string? reason)
    {
        TechnicianAssignmentId = assignmentId; Kind = kind; ActorId = actor;
        ScheduledAt = scheduledAt; ContactName = contactName; ContactPhone = contactPhone;
        Reason = reason; CreatedAt = DateTime.UtcNow;
    }
    public int Id { get; private set; }
    public int TechnicianAssignmentId { get; private set; }
    public string Kind { get; private set; } = string.Empty;
    public int ActorId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ScheduledAt { get; private set; }
    public string? ContactName { get; private set; }
    public string? ContactPhone { get; private set; }
    public string? Reason { get; private set; }
}
