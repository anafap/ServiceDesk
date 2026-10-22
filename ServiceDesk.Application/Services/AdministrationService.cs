using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Application.Services;

public record StoreInput(string StoreCode, string Name, string Address, string City);
public record TechnicianInput(string FullName, string CompanyName, string PhoneNumber, string Specialisation);

public sealed class AdministrationService(IAdministrationRepository repository)
{
    public Task<List<Store>> GetStoresAsync() => repository.GetStoresAsync();
    public Task<List<ExternalTechnician>> GetTechniciansAsync() => repository.GetTechniciansAsync();
    public async Task<Store> SaveStoreAsync(int? id, StoreInput input)
    {
        var store = id.HasValue ? await repository.GetStoreAsync(id.Value) ?? throw new KeyNotFoundException("Store not found.")
            : new Store(input.StoreCode, input.Name, input.Address, input.City);
        store.UpdateDetails(input.StoreCode, input.Name, input.Address, input.City);
        if (await repository.StoreCodeExistsAsync(store.StoreCode, id)) throw new InvalidOperationException("This store code is already in use.");
        if (!id.HasValue) await repository.AddStoreAsync(store);
        await repository.SaveAsync();
        return store;
    }
    public async Task<ExternalTechnician> SaveTechnicianAsync(int? id, TechnicianInput input)
    {
        var technician = id.HasValue ? await repository.GetTechnicianAsync(id.Value) ?? throw new KeyNotFoundException("Technician not found.")
            : new ExternalTechnician(input.FullName, input.CompanyName, input.PhoneNumber, input.Specialisation);
        technician.UpdateDetails(input.FullName, input.CompanyName, input.PhoneNumber, input.Specialisation);
        if (!id.HasValue) await repository.AddTechnicianAsync(technician);
        await repository.SaveAsync();
        return technician;
    }
    public async Task SetStoreActiveAsync(int id, bool active)
    {
        var store = await repository.GetStoreAsync(id) ?? throw new KeyNotFoundException("Store not found.");
        store.SetActive(active);
        await repository.SaveAsync();
    }
    public async Task SetTechnicianActiveAsync(int id, bool active)
    {
        var technician = await repository.GetTechnicianAsync(id) ?? throw new KeyNotFoundException("Technician not found.");
        technician.SetActive(active);
        await repository.SaveAsync();
    }
}
