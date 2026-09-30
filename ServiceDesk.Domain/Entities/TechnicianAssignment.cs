
namespace ServiceDesk.Domain.Entities;

public class TechnicianAssignment
{
    public int Id { get; private set; }

    public int SupportTicketId { get; private set; }

    public int ExternalTechnicianId { get; private set; }

    public int AssignedByUserId { get; private set; }

    public DateTime AssignedAt { get; private set; }

    public DateTime? ScheduledAt { get; private set; }

    public string Notes { get; private set; } = string.Empty;

    public TechnicianAssignment(int supportTicketId, int externalTechnicianId, int assignedByUserId, DateTime? scheduledAt, string notes)
    {
        if (supportTicketId <= 0)
            throw new ArgumentException("SupportTicketId is required.", nameof(supportTicketId));
        if (externalTechnicianId <= 0)
            throw new ArgumentException("externalTechnicianId is required.", nameof(externalTechnicianId));
        if (assignedByUserId <= 0)
            throw new ArgumentException("AssignedByUserId is required.", nameof(assignedByUserId));
        if (scheduledAt.HasValue && scheduledAt.Value < DateTime.UtcNow)
            throw new ArgumentException(
                "Scheduled date cannot be in the past.",
                nameof(scheduledAt));


        SupportTicketId = supportTicketId;
        ExternalTechnicianId = externalTechnicianId;
        AssignedByUserId = assignedByUserId;
        ScheduledAt = scheduledAt;
        AssignedAt = DateTime.UtcNow;
        Notes = notes ?? string.Empty;



    }
    public void Reschedule(DateTime scheduledAt)
    {
        if (scheduledAt < DateTime.UtcNow)
            throw new ArgumentException(
                "Scheduled date cannot be in the past.",
                nameof(scheduledAt));

        ScheduledAt = scheduledAt;
    }


}