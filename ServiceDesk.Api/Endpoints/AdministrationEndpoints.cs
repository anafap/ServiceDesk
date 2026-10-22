using ServiceDesk.Application.Services;
using ServiceDesk.Application.Interfaces;
namespace ServiceDesk.Api.Endpoints;

public static class AdministrationEndpoints
{
    public static void MapAdministrationEndpoints(this WebApplication app)
    {
        var admin = app.MapGroup("/api/admin").RequireAuthorization("Admin").AddEndpointFilter<VersionFilter>();
        admin.MapGet("/stores", async (AdministrationService service) => Results.Ok(await service.GetStoresAsync()));
        admin.MapPost("/stores", (StoreInput input, AdministrationService service) => Handle(async () => {
            var store = await service.SaveStoreAsync(null, input); return Results.Created($"/api/admin/stores/{store.Id}", store);
        }));
        admin.MapPut("/stores/{id:int}", (int id, StoreInput input, AdministrationService service) => Handle(async () => Results.Ok(await service.SaveStoreAsync(id, input))));
        admin.MapPatch("/stores/{id:int}/status", (int id, ActivationInput input, AdministrationService service) => Handle(async () => { await service.SetStoreActiveAsync(id, input.IsActive ?? throw new ArgumentException("isActive is required."), input.ExpectedVersion); return Results.NoContent(); }));
        admin.MapGet("/external-technicians", async (AdministrationService service) => Results.Ok(await service.GetTechniciansAsync()));
        admin.MapPost("/external-technicians", (TechnicianInput input, AdministrationService service) => Handle(async () => {
            var technician = await service.SaveTechnicianAsync(null, input); return Results.Created($"/api/admin/external-technicians/{technician.Id}", technician);
        }));
        admin.MapPut("/external-technicians/{id:int}", (int id, TechnicianInput input, AdministrationService service) => Handle(async () => Results.Ok(await service.SaveTechnicianAsync(id, input))));
        admin.MapPatch("/external-technicians/{id:int}/status", (int id, ActivationInput input, AdministrationService service) => Handle(async () => { await service.SetTechnicianActiveAsync(id, input.IsActive ?? throw new ArgumentException("isActive is required."), input.ExpectedVersion); return Results.NoContent(); }));
    }
    private static Task<IResult> Handle(Func<Task<IResult>> operation) => operation();
}
public record ActivationInput(bool? IsActive, string? ExpectedVersion = null) : IVersionedRequest;
