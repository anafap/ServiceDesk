namespace ServiceDesk.Domain.Entities;
public sealed class Notification
{
    public long Id { get; private set; }
    public int RecipientId { get; private set; }
    public int SupportTicketId { get; private set; }
    public string EventKey { get; private set; } = string.Empty;
    public string Kind { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ReadAt { get; private set; }
    private Notification() { }
    public Notification(int recipientId, int ticketId, string eventKey, string kind)
    { RecipientId = recipientId; SupportTicketId = ticketId; EventKey = eventKey; Kind = kind; CreatedAt = DateTime.UtcNow; }
    public void MarkRead() => ReadAt ??= DateTime.UtcNow;
}
