using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Interfaces;
public interface IAttachmentRepository
{
    Task<List<TicketAttachment>> ListAsync(int ticketId);
    Task<int> CountActiveAsync(int ticketId);
    Task<TicketAttachment?> GetAsync(Guid id);
    Task AddAsync(TicketAttachment item);
    Task SaveAsync();
    Task<TicketAttachment?> ClaimAsync();
    Task ExpireAsync();
    Task<HashSet<string>> ReadyKeysAsync();
}
