using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface ITechnicianAssignmentRepository
{
    Task<TechnicianAssignment?> GetLatestByTicketIdAsync(int ticketId);
    Task AddAsync(TechnicianAssignment assignment);
}
