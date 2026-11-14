using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure;
namespace ServiceDesk.Api.Development;
public static class AdminProvisioner
{
    public static async Task RunAsync(IServiceProvider services, IConfiguration configuration)
    {
        var email = configuration["Provisioning:AdminEmail"]?.Trim();
        var password = configuration["Provisioning:AdminPassword"];
        if (string.IsNullOrWhiteSpace(email) || !System.Net.Mail.MailAddress.TryCreate(email, out var address) || address.Address != email
            || email.Length > 320 || string.IsNullOrWhiteSpace(password) || password.Length is < 8 or > 128)
            throw new InvalidOperationException("Configure a valid Provisioning:AdminEmail and AdminPassword (8–128 characters).");
        using var scope = services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        if (!await db.TicketCategories.AnyAsync())
        {
            var category = new TicketCategory(); db.Entry(category).Property(c => c.Name).CurrentValue = "General maintenance";
            db.TicketCategories.Add(category);
        }
        var normalized = email.ToUpperInvariant();
        var existing = await db.Users.SingleOrDefaultAsync(u => u.NormalizedEmail == normalized);
        if (existing is not null)
        {
            if (existing.Role != UserRole.Admin || !existing.IsActive || string.IsNullOrEmpty(existing.PasswordHash))
                throw new InvalidOperationException("The provisioned email conflicts with an existing account.");
            await db.SaveChangesAsync();
            return;
        }
        var user = new User("Business", "Admin", email, "Not supplied", UserRole.Admin, null);
        user.SetPasswordHash(scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>().HashPassword(user, password));
        db.Users.Add(user); await db.SaveChangesAsync();
    }
}
