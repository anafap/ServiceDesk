using System.Security.Claims;
using ServiceDesk.Api.Security;
using ServiceDesk.Application.Interfaces;
namespace ServiceDesk.Api.Endpoints;
public static class NotificationEndpoints
{
    public static void MapNotificationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/notifications").RequireAuthorization();
        group.MapGet("", async (long? beforeId, ClaimsPrincipal user, INotificationRepository repository) =>
            Results.Ok(await repository.GetAsync(JwtIdentity.UserId(user), JwtIdentity.StoreId(user), beforeId)));
        group.MapPost("/{id:long}/read", async (long id, ClaimsPrincipal user, INotificationRepository repository) =>
            await repository.MarkReadAsync(JwtIdentity.UserId(user), JwtIdentity.StoreId(user), id) ? Results.NoContent() : Results.NotFound());
    }
}
