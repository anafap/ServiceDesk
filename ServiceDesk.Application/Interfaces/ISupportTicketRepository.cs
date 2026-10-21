using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface ISupportTicketRepository
{
    Task<SupportTicket?> GetByIdAsync(int ticketId);
    Task<List<SupportTicket>> GetAllAsync(int? storeId = null);
    Task SaveAsync(SupportTicket ticket);
    Task AddAsync(SupportTicket ticket);
}