using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Services;
public sealed class DiscussionService(ISupportTicketRepository tickets, IDiscussionRepository comments,
    IWorkflowTransaction transaction, INotificationPublisher? notifications = null)
{
    public Task<List<TicketComment>> GetAsync(int id, int? beforeId)
    {
        if (beforeId is <= 0) throw new ArgumentException("Invalid comment cursor.");
        return comments.GetAsync(id, beforeId);
    }
    public async Task<TicketComment> AddAsync(int id, int actor, string text)
    {
        var comment = new TicketComment(id, actor, text);
        await transaction.ExecuteAsync(async () =>
        {
            var ticket = await tickets.GetByIdAsync(id) ?? throw new KeyNotFoundException("Ticket not found.");
            ticket.RecordDiscussionActivity();
            await tickets.SaveAsync(ticket);
            await comments.AddAsync(comment);
            if (notifications is not null) await notifications.PublishAsync(ticket, actor, $"comment:{comment.Id}", "Comment added");
        });
        return comment;
    }
}
