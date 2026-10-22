using ServiceDesk.Domain.Exceptions;
using ServiceDesk.Domain.Enums;
namespace ServiceDesk.Domain.Entities;

public class SupportTicket : VersionedEntity
{
    public int Id { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int CreatedByUserId { get; private set; }
    public int CategoryId { get; private set; }
    public int StoreId { get; private set; }
    public int? AssetId { get; private set; }


    public SupportTicket(string title, string description, int createdByUserId, int categoryId, int storeId,
    TicketPriority priority)
    {
        if (string.IsNullOrWhiteSpace(title))
            throw new ArgumentException("Title is required.", nameof(title));
        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));
        if (createdByUserId <= 0)
            throw new ArgumentException("A valid creator user is required.", nameof(createdByUserId));
        if (storeId <= 0)
            throw new ArgumentException("A storeId is required.", nameof(storeId));
        if (categoryId <= 0)
            throw new ArgumentException("A categoryId is required.", nameof(categoryId));

        if (!Enum.IsDefined(typeof(TicketPriority), priority))
            throw new ArgumentException("Invalid ticket priority.", nameof(priority));

        Title = title;
        Description = description;
        CreatedByUserId = createdByUserId;
        StoreId = storeId;
        CategoryId = categoryId;
        Priority = priority;
        Status = TicketStatus.AwaitingApproval;
        CreatedAt = DateTime.UtcNow;




    }


    public void UpdateDetails(string title, string description, TicketPriority priority)
    {
        if (Status is TicketStatus.Closed or TicketStatus.Rejected)
            throw new StateConflictException("Correct the ticket status before editing a closed or rejected request.");
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            throw new ArgumentException("Title is required and must be 200 characters or fewer.", nameof(title));
        if (string.IsNullOrWhiteSpace(description) || description.Trim().Length > 4000)
            throw new ArgumentException("Description is required and must be 4,000 characters or fewer.", nameof(description));
        if (!Enum.IsDefined(priority)) throw new ArgumentException("Invalid ticket priority.", nameof(priority));
        Title = title.Trim();
        Description = description.Trim();
        Priority = priority;
    }

    public void Approve()
    {
        if (Status != TicketStatus.AwaitingApproval)
        {
            throw new StateConflictException(
                "Only awaiting-approval tickets can be approved.");
        }

        Status = TicketStatus.Approved;
    }

    public void Reject()
    {
        if (Status != TicketStatus.AwaitingApproval)
        {
            throw new StateConflictException(
                "Only AwaitingApproval tickets can be rejected.");
        }

        Status = TicketStatus.Rejected;
    }


    public void Assign()
    {
        if (Status != TicketStatus.Approved)
        {
            throw new StateConflictException(
                "Only Approved tickets can be assigned.");
        }

        Status = TicketStatus.Assigned;
    }
    public void StartProgress()
    {
        if (Status != TicketStatus.Assigned)
        {
            throw new StateConflictException(
                "Only assigned tickets can be moved to in progress.");
        }

        Status = TicketStatus.InProgress;
    }
    public void Resolve()
    {
        if (Status != TicketStatus.InProgress)
        {
            throw new StateConflictException(
                "Only in-progress tickets can be resolved.");
        }

        Status = TicketStatus.Resolved;
    }
    public void Close()
    {
        if (Status != TicketStatus.Resolved)
        {
            throw new StateConflictException(
                "Only Resolved tickets can be closed.");
        }

        Status = TicketStatus.Closed;
    }
    public void CorrectStatus(TicketStatus target)
    {
        var edge = ((int)Status, (int)target);
        if (edge is not ((1, 2) or (2, 1) or (1, 7) or (7, 1) or (2, 3) or (3, 2)
            or (3, 4) or (4, 3) or (4, 5) or (5, 4) or (5, 6) or (6, 5)))
            throw new StateConflictException("The recorded event cannot be reversed from the current status.");
        Status = target;
    }
}
