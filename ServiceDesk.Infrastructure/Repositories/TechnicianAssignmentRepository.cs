
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace ServiceDesk.Infrastructure.Repositories;

public class TechnicianAssignmentRepository : ITechnicianAssignmentRepository
{
    public Task<TechnicianAssignment?> GetLatestByTicketIdAsync(int ticketId) =>
        _dbContext.TechnicianAssignments.AsNoTracking().Where(assignment => assignment.SupportTicketId == ticketId)
            .OrderByDescending(assignment => assignment.AssignedAt).ThenByDescending(assignment => assignment.Id).FirstOrDefaultAsync();

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
