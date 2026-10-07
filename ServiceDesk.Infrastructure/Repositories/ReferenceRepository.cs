using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Application.Interfaces;

namespace ServiceDesk.Infrastructure.Repositories;

public class ReferenceRepository : IReferenceDataRepository
{
    private readonly ServiceDeskDbContext _dbContext;

    public ReferenceRepository(ServiceDeskDbContext dbContext)
    {
        _dbContext = dbContext;
    }


    public async Task<List<Store>> GetStoreAsync()
    {
        return await _dbContext.Stores.AsNoTracking().Where(store => store.IsActive).OrderBy(store => store.Name).ToListAsync();
    }

    public async Task<List<TicketCategory>> GetCategoriesAsync()
    {
        return await _dbContext.TicketCategories.AsNoTracking().OrderBy(TicketCategory => TicketCategory.Name).ToListAsync();
    }

    public async Task<List<ExternalTechnician>> GetTechniciansAsync()
    {
        return await _dbContext.ExternalTechnicians.AsNoTracking().Where(ExternalTechnician => ExternalTechnician.IsActive).OrderBy(ExternalTechnician => ExternalTechnician.FullName).ToListAsync();
    }
}