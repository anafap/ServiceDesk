using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Application.Interfaces;

public interface IReferenceDataRepository
{


    Task<List<Store>> GetStoreAsync();
    Task<List<TicketCategory>> GetCategoriesAsync();
    Task<List<ExternalTechnician>> GetTechniciansAsync();

}
