using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure;

namespace ServiceDesk.Api.Development;

public static class DemoDataSeeder
{
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration)
    {
        // Validate every secret before touching the database. Never reset an existing password.
        var accounts = new[] { "Admin", "Manager1", "Manager2" }.Select(key => (
            Key: key,
            Email: Required(configuration, $"DemoData:{key}:Email").Trim(),
            Password: Required(configuration, $"DemoData:{key}:Password"))).ToArray();
        if (accounts.Select(item => item.Email.ToLowerInvariant()).Distinct().Count() != accounts.Length)
            throw new InvalidOperationException("Demo accounts must use different email addresses.");
        if (accounts.Any(item => item.Password.Length < 12))
            throw new InvalidOperationException("Demo passwords must contain at least 12 characters.");
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var stores = new List<Store>();
        foreach (var number in new[] { 1, 2 })
        {
            var code = $"DEMO-0{number}";
            var name = $"Demo Store {number}";
            var store = await db.Stores.SingleOrDefaultAsync(item => item.StoreCode == code);
            if (store is null) { store = new Store(code, name, "Demo address", "Manama"); db.Stores.Add(store); await db.SaveChangesAsync(); }
            else if (store.Name != name) throw new InvalidOperationException($"Reserved demo store code {code} is already used by another record.");
            stores.Add(store);
        }
        var users = new List<User>();
        foreach (var (account, index) in accounts.Select((account, index) => (account, index)))
        {
            var role = index == 0 ? UserRole.Admin : UserRole.StoreManager;
            int? storeId = index == 0 ? null : stores[index - 1].Id;
            var user = await db.Users.SingleOrDefaultAsync(item => item.Email == account.Email);
            if (user is null)
            {
                user = new User("Demo", account.Key, account.Email, "00000000", role, storeId);
                user.SetPasswordHash(hasher.HashPassword(user, account.Password));
                db.Users.Add(user); await db.SaveChangesAsync();
            }
            else if (user.Role != role || user.StoreId != storeId || string.IsNullOrWhiteSpace(user.PasswordHash))
                throw new InvalidOperationException("An existing account conflicts with demo provisioning. Choose unused demo emails.");
            users.Add(user);
        }
        var category = await db.TicketCategories.FirstOrDefaultAsync(item => item.Name == "Demo maintenance");
        if (category is null)
        {
            category = new TicketCategory();
            db.Entry(category).Property(item => item.Name).CurrentValue = "Demo maintenance";
            db.TicketCategories.Add(category); await db.SaveChangesAsync();
        }
        var technician = await db.ExternalTechnicians.FirstOrDefaultAsync(item => item.FullName == "Demo Portfolio Technician" && item.CompanyName == "ServiceDesk Demo");
        if (technician is null)
        {
            technician = new ExternalTechnician("Demo Portfolio Technician", "ServiceDesk Demo", "00000000", "General maintenance");
            db.ExternalTechnicians.Add(technician); await db.SaveChangesAsync();
        }
        for (var index = 0; index < stores.Count; index++)
        {
            var title = $"[Demo] Store {index + 1} printer repair";
            // Existing manager tickets are sufficient demo data, even after editing their text.
            if (await db.SupportTickets.AnyAsync(item => item.StoreId == stores[index].Id && item.CreatedByUserId == users[index + 1].Id)) continue;
            var marker = $"Portfolio demo fixture DEMO-0{index + 1}.";
            db.SupportTickets.Add(new SupportTicket(title, marker, users[index + 1].Id, category.Id, stores[index].Id, TicketPriority.High));
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
    private static string Required(IConfiguration configuration, string key) =>
        !string.IsNullOrWhiteSpace(configuration[key]) ? configuration[key]! : throw new InvalidOperationException($"Configure {key} before provisioning demo data.");
}
