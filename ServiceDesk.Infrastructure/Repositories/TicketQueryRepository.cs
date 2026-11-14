using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
namespace ServiceDesk.Infrastructure.Repositories;
public sealed class TicketQueryRepository(ServiceDeskDbContext db) : ITicketQueryRepository
{
    private IQueryable<SupportTicket> Scoped(int? storeId) => db.SupportTickets.AsNoTracking().Where(t => !storeId.HasValue || t.StoreId == storeId.Value);
    // Enrich only the already-authorized page, using one SQL query rather than one read per ticket.
    public Task<Dictionary<int, string?>> GetVisitStatesAsync(int[] ticketIds) => db.SupportTickets.AsNoTracking()
        .Where(t => ticketIds.Contains(t.Id)).Select(t => new
        {
            t.Id,
            State = db.TechnicianAssignments.Where(a => a.SupportTicketId == t.Id && a.WithdrawnAt == null)
                .OrderByDescending(a => a.AssignedAt).ThenByDescending(a => a.Id).Select(a => a.VisitState).FirstOrDefault()
        }).ToDictionaryAsync(t => t.Id, t => t.State);

    private IQueryable<int> DeclinedTickets => db.SupportTickets.Where(t => t.Status == TicketStatus.Assigned &&
        db.TechnicianAssignments.Where(a => a.SupportTicketId == t.Id && a.WithdrawnAt == null)
            .OrderByDescending(a => a.AssignedAt).ThenByDescending(a => a.Id).Select(a => a.VisitState).FirstOrDefault() == "Declined").Select(t => t.Id);

    public async Task<TicketPage> SearchAsync(int? storeId, TicketQuery options)
    {
        options.Validate();
        var query = Scoped(storeId);
        if (!string.IsNullOrWhiteSpace(options.Title)) { var title = options.Title.Trim().ToUpperInvariant(); query = query.Where(t => t.Title.ToUpper().Contains(title)); }
        if (options.OpenOnly) query = query.Where(t => t.Status == TicketStatus.AwaitingApproval || t.Status == TicketStatus.Approved || t.Status == TicketStatus.Assigned);
        if (options.Status.HasValue) query = query.Where(t => t.Status == options.Status.Value);
        if (options.Priority.HasValue) query = query.Where(t => t.Priority == options.Priority.Value);
        var count = await query.CountAsync();
        var items = await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id)
            .Skip((options.Page - 1) * options.PageSize).Take(options.PageSize).ToListAsync();
        return new(items, count, options.Page, options.PageSize);
    }
    public async Task<TicketDashboard> DashboardAsync(int? storeId)
    {
        var query = Scoped(storeId);
        var counts = await query.GroupBy(t => 1).Select(g => new TicketCounts(g.Count(),
            g.Count(t => t.Status == TicketStatus.AwaitingApproval || t.Status == TicketStatus.Approved || t.Status == TicketStatus.Assigned),
            g.Count(t => t.Status == TicketStatus.InProgress), g.Count(t => t.Status == TicketStatus.Resolved))).SingleOrDefaultAsync()
            ?? new TicketCounts(0, 0, 0, 0);
        var attention = query.Where(t => storeId.HasValue
            ? t.Status == TicketStatus.Assigned || t.Status == TicketStatus.InProgress
            : t.Status == TicketStatus.AwaitingApproval || t.Status == TicketStatus.Approved || t.Status == TicketStatus.Resolved || DeclinedTickets.Contains(t.Id));
        return new(counts, await query.OrderByDescending(t => t.CreatedAt).ThenByDescending(t => t.Id).Take(5).ToListAsync(),
            await attention.OrderByDescending(t => DeclinedTickets.Contains(t.Id)).ThenByDescending(t => t.Priority).ThenBy(t => t.CreatedAt).ThenBy(t => t.Id).Take(8).ToListAsync());
    }
}
