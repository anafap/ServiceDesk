



using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;

public class ReferenceService
{
    private readonly IReferenceDataRepository _reference;

    public ReferenceService(IReferenceDataRepository reference)
    {
        _reference = reference;

    }

    public async Task<List<Store>> GetAllStoresAsync()
    {
        return await _reference.GetStoreAsync();
    }
    public async Task<List<TicketCategory>> GetAllCategoriesAsync()
    {
        return await _reference.GetCategoriesAsync();
    }
    public async Task<List<ExternalTechnician>> GetAllExternalAsync()
    {
        return await _reference.GetTechniciansAsync();
    }
}