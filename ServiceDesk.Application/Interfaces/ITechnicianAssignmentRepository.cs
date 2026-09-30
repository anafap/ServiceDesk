using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface ITechnicianAssignmentRepository
{
    Task AddAsync(TechnicianAssignment assignment);
}