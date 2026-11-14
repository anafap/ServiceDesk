using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
namespace ServiceDesk.Application.Interfaces;
public record TicketQuery(int Page = 1, int PageSize = 25, string? Title = null, TicketStatus? Status = null, TicketPriority? Priority = null, bool OpenOnly = false)
{
    public void Validate()
    {
        if (Page < 1 || PageSize is < 1 or > 100 || (long)(Page - 1) * PageSize > int.MaxValue ||
            Title?.Length > 200 || Status.HasValue && !Enum.IsDefined(Status.Value) || Priority.HasValue && !Enum.IsDefined(Priority.Value))
            throw new ArgumentException("Invalid ticket filters or pagination.");
    }
}
public record TicketPage(List<SupportTicket> Items, int TotalCount, int Page, int PageSize);
public record TicketCounts(int Total, int Open, int InProgress, int Resolved);
public record TicketDashboard(TicketCounts Summary, List<SupportTicket> Recent, List<SupportTicket> Attention);
public interface ITicketQueryRepository
{
    Task<Dictionary<int, string?>> GetVisitStatesAsync(int[] ticketIds);
    Task<TicketPage> SearchAsync(int? storeId, TicketQuery query);
    Task<TicketDashboard> DashboardAsync(int? storeId);
}
