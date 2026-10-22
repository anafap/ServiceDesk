using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface ITechnicianAssignmentRepository
{
    Task<TechnicianAssignment?> GetLatestByTicketIdAsync(int ticketId);
    Task<TechnicianAssignment?> GetByIdAsync(int id);
    Task SaveAsync(TechnicianAssignment assignment);
    Task AddAsync(TechnicianAssignment assignment);
}
