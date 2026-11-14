using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Interfaces;
public interface INotificationPublisher
{
    Task PublishAsync(SupportTicket ticket, int actor, string eventKey, string kind);
}
