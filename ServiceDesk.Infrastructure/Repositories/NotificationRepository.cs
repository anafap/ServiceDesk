using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
namespace ServiceDesk.Infrastructure.Repositories;
public sealed class NotificationRepository(ServiceDeskDbContext db) : INotificationRepository, INotificationPublisher
{
    public async Task PublishAsync(SupportTicket ticket, int actor, string eventKey, string kind)
    {
        var recipients = await db.Users.Where(u => u.IsActive && u.Id != actor &&
            (u.Role == UserRole.Admin || u.Role == UserRole.StoreManager && u.StoreId == ticket.StoreId)).Select(u => u.Id).ToListAsync();
        foreach (var recipient in recipients)
            db.Notifications.Add(new Notification(recipient, ticket.Id, eventKey, kind));
        await db.SaveChangesAsync();
    }
    private IQueryable<Notification> Visible(int recipient, int? storeId) => db.Notifications.Where(n => n.RecipientId == recipient &&
        db.SupportTickets.Any(t => t.Id == n.SupportTicketId && (!storeId.HasValue || t.StoreId == storeId.Value)));
    public async Task<NotificationPage> GetAsync(int recipient, int? storeId, long? beforeId)
    {
        if (beforeId is <= 0) throw new ArgumentException("Invalid notification cursor.");
        var query = Visible(recipient, storeId);
        var count = await query.CountAsync(n => n.ReadAt == null);
        var items = await query.AsNoTracking().Where(n => !beforeId.HasValue || n.Id < beforeId.Value).OrderByDescending(n => n.Id).Take(26).ToListAsync();
        var page = items.Take(25).ToList();
        return new(page, count, items.Count > 25 ? page.Last().Id : null);
    }
    public async Task<bool> MarkReadAsync(int recipient, int? storeId, long id)
    {
        // Conditional SQL update is idempotent even when requests overlap.
        var query = Visible(recipient, storeId).Where(n => n.Id == id);
        if (!await query.AnyAsync()) return false;
        await query.Where(n => n.ReadAt == null).ExecuteUpdateAsync(set => set.SetProperty(n => n.ReadAt, DateTime.UtcNow));
        return true;
    }
}
