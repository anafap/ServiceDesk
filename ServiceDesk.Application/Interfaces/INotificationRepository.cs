using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Interfaces;
public record NotificationPage(List<Notification> Items, int UnreadCount, long? NextBeforeId);
public interface INotificationRepository
{
    Task<NotificationPage> GetAsync(int recipient, int? storeId, long? beforeId);
    Task<bool> MarkReadAsync(int recipient, int? storeId, long id);
}
