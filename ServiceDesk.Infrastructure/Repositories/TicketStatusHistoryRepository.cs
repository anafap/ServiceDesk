using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Infrastructure;

namespace ServiceDesk.Infrastructure.Repositories;

public class TicketStatusHistoryRepository : ITicketStatusHistoryRepository
{
    private readonly ServiceDeskDbContext _dbContext;

    public TicketStatusHistoryRepository(ServiceDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(TicketStatusHistory history)
    {
        await _dbContext.TicketStatusHistories.AddAsync(history);
        await _dbContext.SaveChangesAsync();
    }
    public async Task<List<TicketStatusHistory>> GetByTicketId(int ticketId)
    {

        return await _dbContext.TicketStatusHistories.Where(history => history.SupportTicketId == ticketId).OrderBy(history => history.ChangedAt).ToListAsync();
    }
}
