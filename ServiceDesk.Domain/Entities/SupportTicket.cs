using ServiceDesk.Domain.Enums;
namespace ServiceDesk.Domain.Entities;

public class SupportTicket
{
    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int CreatedByUserId { get; private set; }
    public int DepartmentId { get; private set; }
    public int CategoryId { get; private set; }
    public int StoreId { get; private set; }
    public int? AssetId { get; private set; }


    public SupportTicket(string title, string description, int createdByUserId, int departmentId, int categoryId, int storeId,
    TicketPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

        Title = title;
        Description = description;
        CreatedByUserId = createdByUserId;
        DepartmentId = departmentId;
        StoreId = storeId;
        CategoryId = categoryId;
        Priority = priority;
        Status = TicketStatus.AwaitingApproval;
        CreatedAt = DateTime.UtcNow;




    }


    public void Approve()
    {
        if (Status != TicketStatus.AwaitingApproval)
        {
            throw new InvalidOperationException(
                "Only awaiting-approval tickets can be approved.");
        }

        Status = TicketStatus.Approved;
    }

    public void Reject()
    {
        if (Status != TicketStatus.AwaitingApproval)
        {
            throw new InvalidOperationException(
                "Only AwaitingApproval tickets can be rejected.");
        }

        Status = TicketStatus.Rejected;
    }


    public void Assign()
    {
        if (Status != TicketStatus.Approved)
        {
            throw new InvalidOperationException(
                "Only Approved tickets can be assigned.");
        }

        Status = TicketStatus.Assigned;
    }
    public void StartProgress()
    {
        if (Status != TicketStatus.Assigned)
        {
            throw new InvalidOperationException(
                "Only assigned tickets can be moved to in progress.");
        }

        Status = TicketStatus.InProgress;
    }
    public void Resolve()
    {
        if (Status != TicketStatus.InProgress)
        {
            throw new InvalidOperationException(
                "Only in-progress tickets can be resolved.");
        }

        Status = TicketStatus.Resolved;
    }
    public void Close()
    {
        if (Status != TicketStatus.Resolved)
        {
            throw new InvalidOperationException(
                "Only Resolved tickets can be closed.");
        }

        Status = TicketStatus.Closed;
    }
}