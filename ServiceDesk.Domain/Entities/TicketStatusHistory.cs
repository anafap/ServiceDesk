namespace ServiceDesk.Domain.Entities;

using ServiceDesk.Domain.Enums;

public class TicketStatusHistory
{


    public int Id { get; private set; }
    public int SupportTicketId { get; private set; }
    public TicketStatus? PreviousStatus { get; private set; }
    public TicketStatus NewStatus { get; private set; }
    public int ChangedByUserId { get; private set; }
    public DateTime ChangedAt { get; private set; }
    public string Reason { get; private set; } = string.Empty;

    public TicketStatusHistory(int supportTicketId, TicketStatus? previousStatus, TicketStatus newStatus, int changedByUserId, string? reason)
    {
        if (supportTicketId <= 0)
            throw new ArgumentException("SupportTicketId is required.", nameof(supportTicketId));
        if (changedByUserId <= 0)
            throw new ArgumentException("ChangedByUserId is required.", nameof(changedByUserId));
        if (!Enum.IsDefined(typeof(TicketStatus), newStatus))
            throw new ArgumentException("Invalid new status.",
            nameof(newStatus));

        SupportTicketId = supportTicketId;
        PreviousStatus = previousStatus;
        NewStatus = newStatus;
        ChangedByUserId = changedByUserId;
        ChangedAt = DateTime.UtcNow;
        Reason = reason ?? string.Empty;
    }


}




