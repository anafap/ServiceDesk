using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Interfaces;
public interface IDiscussionRepository
{
    Task<List<TicketComment>> GetAsync(int ticketId, int? beforeId);
    Task AddAsync(TicketComment comment);
}
