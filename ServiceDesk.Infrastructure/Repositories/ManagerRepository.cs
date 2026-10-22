using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Domain.Exceptions;
namespace ServiceDesk.Infrastructure.Repositories;

public sealed class ManagerRepository(ServiceDeskDbContext db) : IManagerRepository
{
    public Task<List<User>> GetManagersAsync() => db.Users.AsNoTracking().Where(user => user.Role == UserRole.StoreManager).OrderBy(user => user.FirstName).ThenBy(user => user.Id).ToListAsync();
    public Task<User?> GetManagerAsync(int id) => db.Users.SingleOrDefaultAsync(user => user.Id == id && user.Role == UserRole.StoreManager);
    public Task<bool> EmailExistsAsync(string normalizedEmail, int? exceptId) => db.Users.AnyAsync(user => user.NormalizedEmail == normalizedEmail && (!exceptId.HasValue || user.Id != exceptId.Value));
    public Task<bool> IsActiveStoreAsync(int storeId) => db.Stores.AnyAsync(store => store.Id == storeId && store.IsActive);
    public async Task AddAsync(User user) => await db.Users.AddAsync(user);
    public async Task SaveAsync()
    {
        try { await db.SaveChangesAsync(); }
        catch (DbUpdateException error) when (error.InnerException is SqlException { Number: 2601 or 2627 })
        { throw new StateConflictException("This email address is already in use."); }
    }
}
