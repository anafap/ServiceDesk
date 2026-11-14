namespace ServiceDesk.Domain.Entities;
public sealed class TicketAttachment : VersionedEntity
{
    public Guid Id { get; private set; } = Guid.NewGuid();
    public int SupportTicketId { get; private set; }
    public int AuthorId { get; private set; }
    public string State { get; private set; } = "pending";
    public DateTime CreatedAt { get; private set; } = DateTime.UtcNow;
    public DateTime? LeaseUntil { get; private set; }
    public Guid? LeaseId { get; private set; }
    public int Attempts { get; private set; }
    public string? ContentKey { get; private set; }
    private TicketAttachment() { }
    public TicketAttachment(int ticketId, int authorId)
    {
        if (ticketId <= 0 || authorId <= 0) throw new ArgumentException("A ticket and author are required.");
        SupportTicketId = ticketId; AuthorId = authorId;
    }
    public void Complete(string key) { State = "ready"; ContentKey = key; LeaseUntil = null; }
    public void Reject() { State = "rejected"; LeaseUntil = null; ContentKey = null; }
}
