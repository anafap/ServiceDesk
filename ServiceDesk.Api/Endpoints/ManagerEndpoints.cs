using ServiceDesk.Application.Interfaces;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Api.Endpoints;

public static class ManagerEndpoints
{
    public static void MapManagerEndpoints(this WebApplication app)
    {
        var managers = app.MapGroup("/api/admin/store-managers").RequireAuthorization("Admin").AddEndpointFilter<VersionFilter>();
        managers.MapGet("", async (ManagerService service, IDisplayNameRepository display) =>
        {
            var users = await service.GetAllAsync();
            var names = await display.GetAsync(users.Select(user => user.StoreId!.Value).Distinct().ToArray(), [], [], []);
            return Results.Ok(users.Select(user => ManagerResponse.From(user, names)));
        });
        managers.MapPost("", async (ManagerInput input, ManagerService service, IDisplayNameRepository display) =>
        {
            var user = await service.SaveAsync(null, input);
            var names = await display.GetAsync([user.StoreId!.Value], [], [], []);
            return Results.Created($"/api/admin/store-managers/{user.Id}", ManagerResponse.From(user, names));
        });
        managers.MapPut("/{id:int}", async (int id, ManagerInput input, ManagerService service, IDisplayNameRepository display) =>
        {
            var user = await service.SaveAsync(id, input);
            var names = await display.GetAsync([user.StoreId!.Value], [], [], []);
            return Results.Ok(ManagerResponse.From(user, names));
        });
        managers.MapPatch("/{id:int}/status", async (int id, ActivationInput input, ManagerService service) =>
        {
            await service.SetActiveAsync(id, input.IsActive ?? throw new ArgumentException("isActive is required."), input.ExpectedVersion!);
            return Results.NoContent();
        });
        managers.MapPost("/{id:int}/reset-password", async (int id, ResetPasswordInput input, ManagerService service) =>
        {
            await service.ResetPasswordAsync(id, input.Password, input.ExpectedVersion!);
            return Results.NoContent();
        });
    }
}
public record ResetPasswordInput(string Password, string? ExpectedVersion = null) : IVersionedRequest;
public record ManagerResponse(int Id, string FirstName, string LastName, string Email, string PhoneNumber,
    int StoreId, string? StoreName, bool IsActive, string Version)
{
    public static ManagerResponse From(User user, DisplayNames names) => new(user.Id, user.FirstName, user.LastName, user.Email,
        user.PhoneNumber, user.StoreId!.Value, names.Stores.GetValueOrDefault(user.StoreId.Value), user.IsActive, user.Version);
}
