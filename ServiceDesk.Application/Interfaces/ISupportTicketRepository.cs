using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface ISupportTicketRepository
{
    Task<SupportTicket?> GetByIdAsync(int ticketId);
    Task SaveAsync(SupportTicket ticket);
    Task AddAsync(SupportTicket ticket);
}