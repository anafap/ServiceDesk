
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Infrastructure;

namespace ServiceDesk.Infrastructure.Repositories;

public class TechnicianAssignmentRepository : ITechnicianAssignmentRepository
{

    private readonly ServiceDeskDbContext _dbContext;
    public TechnicianAssignmentRepository(ServiceDeskDbContext dbContext)
    {
        _dbContext = dbContext;

    }

    public async Task AddAsync(TechnicianAssignment technicianAssignment)
    {
        await _dbContext.TechnicianAssignments.AddAsync(technicianAssignment);
        await _dbContext.SaveChangesAsync();
    }

}