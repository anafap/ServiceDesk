using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Infrastructure.Repositories;
public sealed class DiscussionRepository(ServiceDeskDbContext db) : IDiscussionRepository
{
    public Task<List<TicketComment>> GetAsync(int ticketId, int? beforeId) => db.TicketComments.AsNoTracking()
        .Where(c => c.SupportTicketId == ticketId && (!beforeId.HasValue || c.Id < beforeId.Value))
        .OrderByDescending(c => c.Id).Take(26).ToListAsync();
    public async Task AddAsync(TicketComment comment) { db.TicketComments.Add(comment); await db.SaveChangesAsync(); }
}
