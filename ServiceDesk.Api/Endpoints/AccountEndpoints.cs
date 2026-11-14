using System.Security.Claims;
using ServiceDesk.Api.Security;
using ServiceDesk.Application.Services;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Api.Endpoints;
public static class AccountEndpoints
{
    public static void MapAccountEndpoints(this WebApplication app)
    {
        var account = app.MapGroup("/api/account").RequireAuthorization().AddEndpointFilter<DemoAccountReadOnlyFilter>();
        account.MapGet("", async (ClaimsPrincipal principal, AccountService service, IDisplayNameRepository display) =>
            Results.Ok(await Response(await service.GetAsync(JwtIdentity.UserId(principal)), display)));
        account.MapPut("/profile", async (ProfileInput input, ClaimsPrincipal principal, AccountService service, IDisplayNameRepository display) =>
            Results.Ok(await Response(await service.UpdateProfileAsync(JwtIdentity.UserId(principal), input.FirstName, input.LastName, input.PhoneNumber, input.ExpectedVersion), display)));
        account.MapPost("/change-password", async (ChangePasswordInput input, ClaimsPrincipal principal, AccountService service) =>
        {
            await service.ChangePasswordAsync(JwtIdentity.UserId(principal), input.CurrentPassword, input.NewPassword);
            return Results.NoContent();
        }).RequireRateLimiting("login");
    }
    private static async Task<object> Response(User user, IDisplayNameRepository display)
    {
        var names = await display.GetAsync(user.StoreId.HasValue ? [user.StoreId.Value] : [], [], [], []);
        return new { user.Id, user.FirstName, user.LastName, user.Email, user.PhoneNumber,
            Role = user.Role.ToString(), user.StoreId, StoreName = user.StoreId.HasValue ? names.Stores.GetValueOrDefault(user.StoreId.Value) : null, user.Version };
    }
}
public record ChangePasswordInput(string CurrentPassword, string NewPassword);
public record ProfileInput(string FirstName, string LastName, string PhoneNumber, string ExpectedVersion);
