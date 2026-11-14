using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Infrastructure.Repositories;
public sealed class AttachmentRepository(ServiceDeskDbContext db) : IAttachmentRepository
{
    public Task<List<TicketAttachment>> ListAsync(int id) => db.TicketAttachments.AsNoTracking().Where(a => a.SupportTicketId == id).OrderByDescending(a => a.CreatedAt).Take(100).ToListAsync();
    public Task<int> CountActiveAsync(int id) => db.TicketAttachments.CountAsync(a => a.SupportTicketId == id && a.State != "rejected");
    public Task<TicketAttachment?> GetAsync(Guid id) => db.TicketAttachments.SingleOrDefaultAsync(a => a.Id == id);
    public async Task AddAsync(TicketAttachment item) { db.TicketAttachments.Add(item); await db.SaveChangesAsync(); }
    public Task SaveAsync() => db.SaveChangesAsync();
    public async Task<TicketAttachment?> ClaimAsync()
    {
        var now = DateTime.UtcNow;
        var query = db.TicketAttachments.Where(a => a.CreatedAt > now.AddHours(-24) && a.Attempts < 3 &&
            (a.State == "pending" || a.State == "processing" && a.LeaseUntil < now));
        var id = await query.OrderBy(a => a.CreatedAt).Select(a => (Guid?)a.Id).FirstOrDefaultAsync();
        if (id is null) return null;
        var lease = Guid.NewGuid();
        var changed = await query.Where(a => a.Id == id).ExecuteUpdateAsync(set => set.SetProperty(a => a.State, "processing")
            .SetProperty(a => a.LeaseId, lease).SetProperty(a => a.LeaseUntil, now.AddMinutes(3)).SetProperty(a => a.Attempts, a => a.Attempts + 1));
        return changed == 1 ? await db.TicketAttachments.SingleAsync(a => a.Id == id && a.LeaseId == lease) : null;
    }
    public async Task<HashSet<string>> ReadyKeysAsync() => (await db.TicketAttachments.Where(a => a.ContentKey != null).Select(a => a.ContentKey!).ToListAsync()).ToHashSet();
    public Task ExpireAsync()
    {
        var now = DateTime.UtcNow;
        return db.TicketAttachments.Where(a => (a.State == "pending" || a.State == "processing") &&
            (a.CreatedAt < now.AddHours(-24) || a.Attempts >= 3 && a.LeaseUntil < now))
            .ExecuteUpdateAsync(set => set.SetProperty(a => a.State, "rejected").SetProperty(a => a.LeaseUntil, (DateTime?)null));
    }
}
