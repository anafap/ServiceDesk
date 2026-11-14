namespace ServiceDesk.Domain.Entities;

public sealed class TicketComment
{
    public int Id { get; private set; }
    public int SupportTicketId { get; private set; }
    public int AuthorId { get; private set; }
    public string Text { get; private set; } = string.Empty;
    public DateTime CreatedAt { get; private set; }
    private TicketComment() { }
    public TicketComment(int ticketId, int authorId, string text)
    {
        if (ticketId <= 0 || authorId <= 0) throw new ArgumentException("A ticket and author are required.");
        if (string.IsNullOrWhiteSpace(text) || text.Trim().Length > 2000)
            throw new ArgumentException("A comment of 1–2,000 characters is required.");
        SupportTicketId = ticketId; AuthorId = authorId; Text = text.Trim(); CreatedAt = DateTime.UtcNow;
    }
}
