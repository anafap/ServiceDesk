using ServiceDesk.Domain.Enums;
namespace ServiceDesk.Domain.Entities;

public class SupportTicket{
    public int Id {get;private set;}
    public string Title { get; private set; } = string.Empty;
    public string  Description { get; private set; } = string.Empty;
    public TicketStatus Status { get; private set; }
    public TicketPriority Priority { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public int CreatedByUserId { get; private set; }
    public int DepartmentId { get; private set; }
    public int CategoryId { get; private set; }


public SupportTicket(string title, string description,int createdByUserId,int departmentId,int categoryId,
TicketPriority priority){
    if(string.IsNullOrWhiteSpace(title))
        throw new ArgumentException("Title is required.",nameof(title));
    if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description is required.", nameof(description));

    Title = title;
    Description = description;
    CreatedByUserId = createdByUserId;
    DepartmentId = departmentId;
    Priority = priority;
    Status = TicketStatus.Open;
    CreatedAt = DateTime.UtcNow;
    

    }
}