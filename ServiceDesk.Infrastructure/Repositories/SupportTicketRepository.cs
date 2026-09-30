using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Application.Interfaces;

namespace ServiceDesk.Infrastructure.Repositories;

public class SupportTicketRepository : ISupportTicketRepository
{

    private readonly ServiceDeskDbContext _dbContext;

    public SupportTicketRepository(ServiceDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<SupportTicket?> GetByIdAsync(int ticketId)
    {
        return await _dbContext.SupportTickets.SingleOrDefaultAsync(ticket => ticket.Id == ticketId);
    }

    public async Task AddAsync(SupportTicket ticket)
    {
        await _dbContext.SupportTickets.AddAsync(ticket);
        await _dbContext.SaveChangesAsync();
    }

    public async Task SaveAsync(SupportTicket ticket)
    {
        _dbContext.SupportTickets.Update(ticket);
        await _dbContext.SaveChangesAsync();
    }

    public async Task<List<SupportTicket>> GetAllAsync()
    {
        return await _dbContext.SupportTickets.OrderByDescending(ticket => ticket.CreatedAt).ToListAsync();
    }
}

