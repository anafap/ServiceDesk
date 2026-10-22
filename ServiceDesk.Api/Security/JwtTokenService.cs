using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;

namespace ServiceDesk.Api.Security;

public sealed class JwtSettings
{
    public string Key { get; set; } = "";
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
}

public record AuthUser(int Id, string Name, string Role, int? StoreId, string? StoreName = null);
public record LoginResponse(string AccessToken, string TokenType, DateTime ExpiresAt, AuthUser User);

public sealed class JwtTokenService(IOptions<JwtSettings> configured)
{
    public LoginResponse Issue(User user)
    {
        var settings = configured.Value;
        var now = DateTime.UtcNow;
        var expiresAt = now.AddMinutes(30);
        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString(CultureInfo.InvariantCulture)),
            new("role", user.Role.ToString()),
            new("jti", Guid.NewGuid().ToString())
        };
        if (user.StoreId.HasValue) claims.Add(new("store_id", user.StoreId.Value.ToString(CultureInfo.InvariantCulture)));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expiresAt,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", expiresAt,
            new(user.Id, user.FullName, user.Role.ToString(), user.StoreId));
    }
}

public static class JwtIdentity
{
    public static bool HasValidClaims(ClaimsPrincipal user)
    {
        var roles = user.FindAll("role").ToArray();
        if (user.FindAll("sub").Count() != 1 || !PositiveId(user.FindFirstValue("sub")) || roles.Length != 1) return false;
        if (roles[0].Value == nameof(UserRole.Admin)) return true;
        return roles[0].Value == nameof(UserRole.StoreManager)
            && user.FindAll("store_id").Count() == 1 && PositiveId(user.FindFirstValue("store_id"));
    }

    public static int UserId(ClaimsPrincipal user) => int.Parse(user.FindFirstValue("sub")!, CultureInfo.InvariantCulture);
    public static int? StoreId(ClaimsPrincipal user) => user.IsInRole(nameof(UserRole.Admin))
        ? null : int.Parse(user.FindFirstValue("store_id")!, CultureInfo.InvariantCulture);
    public static bool CanAccessStore(ClaimsPrincipal user, int storeId) =>
        user.IsInRole(nameof(UserRole.Admin)) || StoreId(user) == storeId;

    private static bool PositiveId(string? value) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var id) && id > 0;
}
