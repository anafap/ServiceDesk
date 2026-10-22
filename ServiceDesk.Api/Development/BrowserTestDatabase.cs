using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Infrastructure;
namespace ServiceDesk.Api.Development;

// Explicit Development-only browser harness. The configured database name is never used.
public static class BrowserTestDatabase
{
    public static bool Configure(WebApplicationBuilder builder, string[] args)
    {
        var enabled = args.Contains("--browser-test-host") || args.Contains("--cleanup-browser-tests");
        if (!enabled) return false;
        if (!builder.Environment.IsDevelopment() || !Guid.TryParseExact(Environment.GetEnvironmentVariable("SERVICEDESK_BROWSER_RUN_ID"), "N", out var run))
            throw new InvalidOperationException("Browser test mode requires Development and a unique run ID.");
        var connection = new SqlConnectionStringBuilder(builder.Configuration.GetConnectionString("DefaultConnection"))
        { InitialCatalog = $"ServiceDesk_BrowserTests_{run:N}", Pooling = false };
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?> { ["ConnectionStrings:DefaultConnection"] = connection.ConnectionString });
        return true;
    }
    public static async Task PrepareAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>().Database.MigrateAsync();
    }
    public static async Task DeleteAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ServiceDeskDbContext>().Database.EnsureDeletedAsync();
    }
}
