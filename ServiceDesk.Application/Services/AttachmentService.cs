using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Exceptions;
namespace ServiceDesk.Application.Services;
public sealed class AttachmentService(ISupportTicketRepository tickets, IAttachmentRepository repository,
    IPhotoStorage storage, IWorkflowTransaction transaction, INotificationPublisher notifications)
{
    public Task<List<TicketAttachment>> ListAsync(int ticketId) => repository.ListAsync(ticketId);
    public async Task<TicketAttachment> UploadAsync(int ticketId, int actor, Stream source, CancellationToken cancellationToken)
    {
        var item = new TicketAttachment(ticketId, actor);
        var inserted = false;
        try
        {
            await storage.PutOriginalAsync(item.Id, source, cancellationToken);
            await transaction.ExecuteAsync(async () =>
            {
                var ticket = await tickets.GetByIdAsync(ticketId) ?? throw new KeyNotFoundException("Ticket not found.");
                ticket.RecordDiscussionActivity();
                // Updating the ticket first serializes quota checks and detects competing closure.
                await tickets.SaveAsync(ticket);
                if (await repository.CountActiveAsync(ticketId) >= 5) throw new StateConflictException("This ticket already has five photo attachments.");
                await repository.AddAsync(item);
                inserted = true;
            });
            return item;
        }
        catch { if (!inserted) storage.DeleteOriginal(item.Id); throw; }
    }
    public async Task CompleteAsync(Guid id, Guid lease, string key)
    {
        await transaction.ExecuteAsync(async () =>
        {
            var item = await repository.GetAsync(id) ?? throw new KeyNotFoundException("Attachment not found.");
            if (item.State != "processing" || item.LeaseId != lease || item.LeaseUntil <= DateTime.UtcNow)
                throw new StateConflictException("The processing lease expired.");
            var ticket = await tickets.GetByIdAsync(item.SupportTicketId) ?? throw new KeyNotFoundException("Ticket not found.");
            item.Complete(key); await repository.SaveAsync();
            await notifications.PublishAsync(ticket, item.AuthorId, $"photo:{item.Id:N}", "Photo added");
        });
    }
}
