using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceDesk.Api.Development;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
namespace ServiceDesk.Tests;

public class DemoDataTests
{
    [Fact]
    public async Task Demo_seed_rejects_seven_character_password_before_database_access()
    {
        var values = new Dictionary<string, string?>();
        foreach (var key in new[] { "Admin", "Manager1", "Manager2" })
        {
            values[$"DemoData:{key}:Email"] = $"{key.ToLowerInvariant()}@example.test";
            values[$"DemoData:{key}:Password"] = Guid.NewGuid().ToString("N")[..8];
        }
        values["DemoData:Manager1:Password"] = Guid.NewGuid().ToString("N")[..7];
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        await using var provider = new ServiceCollection().BuildServiceProvider();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => DemoDataSeeder.SeedAsync(provider, configuration));
        Assert.Equal("Demo passwords must contain at least 8 characters.", error.Message);
    }

    [SqlFact]
    public async Task Demo_seed_is_repeatable_preserves_edited_tickets_and_passwords_and_resolves_inactive_names()
    {
        await using var database = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        var services = new ServiceCollection();
        services.AddScoped(_ => database.OpenContext());
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        await using var provider = services.BuildServiceProvider();
        var values = new Dictionary<string, string?>();
        foreach (var key in new[] { "Admin", "Manager1", "Manager2" })
        {
            values[$"DemoData:{key}:Email"] = $"{key.ToLowerInvariant()}@example.test";
            values[$"DemoData:{key}:Password"] = Guid.NewGuid().ToString("N")[..8];
        }
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();
        await DemoDataSeeder.SeedAsync(provider, configuration);
        await using var context = database.OpenContext();
        var managers = await context.Users.Where(item => item.Role == UserRole.StoreManager).ToListAsync();
        Assert.Equal(2, managers.Count);
        Assert.NotEqual(managers[0].StoreId, managers[1].StoreId);
        foreach (var manager in managers)
        {
            var password = values[$"DemoData:{(manager.Email.StartsWith("manager1") ? "Manager1" : "Manager2")}:Password"]!;
            Assert.Equal(8, password.Length);
            Assert.NotEqual(password, manager.PasswordHash);
            Assert.NotEqual(PasswordVerificationResult.Failed, new PasswordHasher<User>().VerifyHashedPassword(manager, manager.PasswordHash!, password));
        }
        var hashes = managers.ToDictionary(item => item.Id, item => item.PasswordHash);
        foreach (var ticket in await context.SupportTickets.ToListAsync()) ticket.UpdateDetails("Edited", "Edited description", TicketPriority.Low);
        foreach (var store in await context.Stores.Where(item => item.StoreCode.StartsWith("DEMO-")).ToListAsync()) store.SetActive(false);
        await context.SaveChangesAsync();
        configuration["DemoData:Manager1:Password"] = Guid.NewGuid().ToString("N");
        await DemoDataSeeder.SeedAsync(provider, configuration);
        context.ChangeTracker.Clear();
        Assert.Equal(2, await context.SupportTickets.CountAsync());
        Assert.Equal(3, await context.Users.CountAsync());
        foreach (var manager in await context.Users.Where(item => item.Role == UserRole.StoreManager).ToListAsync()) Assert.Equal(hashes[manager.Id], manager.PasswordHash);
        var names = await new DisplayNameRepository(context).GetAsync(managers.Select(item => item.StoreId!.Value).ToArray(), [], managers.Select(item => item.Id).ToArray(), [int.MaxValue]);
        Assert.Equal(2, names.Stores.Count);
        Assert.Equal(2, names.Users.Count);
        Assert.Empty(names.Technicians);
        Assert.Contains("Demo Store 1", names.Stores.Values);
    }
}
