using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Interfaces;
public interface IAccountRepository
{
    Task<User?> GetTrackedAsync(int id);
    Task SaveAsync();
}
