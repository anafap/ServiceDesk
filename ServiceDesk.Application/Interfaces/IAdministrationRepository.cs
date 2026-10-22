using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Interfaces;

public interface IAdministrationRepository
{
    Task<List<Store>> GetStoresAsync();
    Task<Store?> GetStoreAsync(int id);
    Task<bool> StoreCodeExistsAsync(string code, int? exceptId);
    Task AddStoreAsync(Store store);
    Task<List<ExternalTechnician>> GetTechniciansAsync();
    Task<ExternalTechnician?> GetTechnicianAsync(int id);
    Task AddTechnicianAsync(ExternalTechnician technician);
    Task SaveAsync();
}
