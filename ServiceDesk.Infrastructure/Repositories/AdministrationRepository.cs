using ServiceDesk.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Data.SqlClient;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Infrastructure.Repositories;

public sealed class AdministrationRepository(ServiceDeskDbContext db) : IAdministrationRepository
{
    public Task<List<Store>> GetStoresAsync() => db.Stores.AsNoTracking().OrderBy(store => store.Name).ToListAsync();
    public Task<Store?> GetStoreAsync(int id) => db.Stores.SingleOrDefaultAsync(store => store.Id == id);
    public Task<bool> StoreCodeExistsAsync(string code, int? exceptId) => db.Stores.AnyAsync(store => store.StoreCode.ToUpper() == code && (!exceptId.HasValue || store.Id != exceptId.Value));
    public async Task AddStoreAsync(Store store) => await db.Stores.AddAsync(store);
    public Task<List<ExternalTechnician>> GetTechniciansAsync() => db.ExternalTechnicians.AsNoTracking().OrderBy(item => item.FullName).ToListAsync();
    public Task<ExternalTechnician?> GetTechnicianAsync(int id) => db.ExternalTechnicians.SingleOrDefaultAsync(item => item.Id == id);
    public async Task AddTechnicianAsync(ExternalTechnician technician) => await db.ExternalTechnicians.AddAsync(technician);
    public async Task SaveAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new StateConflictException("This store code is already in use."); }
    }
}
