using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Infrastructure.Repositories;
public sealed class AccountRepository(ServiceDeskDbContext db) : IAccountRepository
{
    public Task<User?> GetTrackedAsync(int id) => db.Users.SingleOrDefaultAsync(u => u.Id == id);
    public Task SaveAsync() => db.SaveChangesAsync();
}
