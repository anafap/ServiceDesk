using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Interfaces;

public interface IManagerRepository
{
    Task<List<User>> GetManagersAsync();
    Task<User?> GetManagerAsync(int id);
    Task<bool> EmailExistsAsync(string normalizedEmail, int? exceptId);
    Task<bool> IsActiveStoreAsync(int storeId);
    Task AddAsync(User user);
    Task SaveAsync();
}
