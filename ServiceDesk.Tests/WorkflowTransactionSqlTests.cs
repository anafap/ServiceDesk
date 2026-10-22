using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Services;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Infrastructure.Transactions;

namespace ServiceDesk.Tests;

public sealed class WorkflowTransactionSqlTests
{
    public static bool SqlConfigured => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SERVICEDESK_TEST_SQL_CONNECTION"));

    [SqlTheory]
    [InlineData(TicketStatus.Approved, TicketStatus.AwaitingApproval)]
    [InlineData(TicketStatus.Rejected, TicketStatus.AwaitingApproval)]
    [InlineData(TicketStatus.Assigned, TicketStatus.Approved)]
    [InlineData(TicketStatus.InProgress, TicketStatus.Assigned)]
    [InlineData(TicketStatus.Resolved, TicketStatus.InProgress)]
    [InlineData(TicketStatus.Closed, TicketStatus.Resolved)]
    public async Task Failed_history_insert_rolls_back_entire_transition_and_allows_retry(TicketStatus next, TicketStatus previous)
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.OpenContext();
        var ticket = await database.AddTicketAsync(context, previous);
        // Force a real SQL failure after the earlier ticket/assignment writes succeed.
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE [TicketStatusHistories] ADD CONSTRAINT [CK_Test_HistoryFailure] CHECK ([ChangedByUserId] <> 9999)");
        var service = MakeService(context);

        await Assert.ThrowsAsync<DbUpdateException>(() => Transition(service, ticket.Id, next, 9999));

        await using (var verification = database.OpenContext())
        {
            Assert.Equal(previous, (await verification.SupportTickets.SingleAsync()).Status);
            Assert.Empty(await verification.TicketStatusHistories.ToListAsync());
            Assert.Empty(await verification.TechnicianAssignments.ToListAsync());
        }
        // The request context must not return the rolled-back state from EF's tracker.
        Assert.Equal(previous, (await service.GetTicketByIdAsync(ticket.Id))!.Status);
        await Transition(service, ticket.Id, next, 1);
        await using var afterRetry = database.OpenContext();
        Assert.Equal(next, (await afterRetry.SupportTickets.SingleAsync()).Status);
        var history = await afterRetry.TicketStatusHistories.SingleAsync();
        Assert.Equal(previous, history.PreviousStatus);
        Assert.Equal(next, history.NewStatus);
        Assert.Equal(1, history.ChangedByUserId);
        Assert.Equal(next == TicketStatus.Assigned ? 1 : 0, await afterRetry.TechnicianAssignments.CountAsync());
    }

    [SqlFact]
    public async Task Failed_assignment_insert_rolls_back_ticket_without_creating_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.OpenContext();
        var ticket = await database.AddTicketAsync(context, TicketStatus.Approved);
        await context.Database.ExecuteSqlRawAsync("ALTER TABLE [TechnicianAssignments] ADD CONSTRAINT [CK_Test_AssignmentFailure] CHECK ([ExternalTechnicianId] <> 9999)");
        var service = MakeService(context);

        await Assert.ThrowsAsync<DbUpdateException>(() => service.AssignTicketAsync(ticket.Id, 9999, 1, DateTime.UtcNow.AddDays(1), "Test", 1, "Test"));

        await using var verification = database.OpenContext();
        Assert.Equal(TicketStatus.Approved, (await verification.SupportTickets.SingleAsync()).Status);
        Assert.Empty(await verification.TechnicianAssignments.ToListAsync());
        Assert.Empty(await verification.TicketStatusHistories.ToListAsync());
    }

    [SqlFact]
    public async Task Complete_workflow_commits_ticket_assignment_and_history()
    {
        await using var database = await TestDatabase.CreateAsync();
        await using var context = database.OpenContext();
        var service = MakeService(context);
        var ticket = await service.CreateTicketAsync("SQL workflow", "Disposable test", 1, database.CategoryId, database.StoreId, TicketPriority.High);
        await service.UpdateTicketDetailsAsync(ticket.Id, "Edited SQL workflow", "Updated", TicketPriority.Critical);
        foreach (var next in new[] { TicketStatus.Approved, TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed })
            await Transition(service, ticket.Id, next, 1);

        await using var verification = database.OpenContext();
        var saved = await verification.SupportTickets.SingleAsync();
        Assert.Equal(TicketStatus.Closed, saved.Status);
        Assert.Equal("Edited SQL workflow", saved.Title);
        Assert.Equal(TicketPriority.Critical, saved.Priority);
        Assert.Equal(ticket.Id, (await verification.TechnicianAssignments.SingleAsync()).SupportTicketId);
        var histories = await verification.TicketStatusHistories.OrderBy(item => item.Id).ToListAsync();
        Assert.Equal(new[] { TicketStatus.Approved, TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Resolved, TicketStatus.Closed }, histories.Select(item => item.NewStatus));
        Assert.All(histories, item => Assert.Equal(1, item.ChangedByUserId));
    }

    private static TicketService MakeService(ServiceDeskDbContext context) => new(
        new SupportTicketRepository(context), new TechnicianAssignmentRepository(context),
        new TicketStatusHistoryRepository(context), new WorkflowTransaction(context));

    private static Task Transition(TicketService service, int id, TicketStatus next, int actor) => next switch
    {
        TicketStatus.Approved => service.ApproveTicketAsync(id, actor, "Test"),
        TicketStatus.Rejected => service.RejectTicketAsync(id, actor, "Test"),
        TicketStatus.Assigned => service.AssignTicketAsync(id, 1, actor, DateTime.UtcNow.AddDays(1), "Test", actor, "Test"),
        TicketStatus.InProgress => service.StartProgressTicketAsync(id, actor, "Test"),
        TicketStatus.Resolved => service.ResolveTicketAsync(id, actor, "Test"),
        TicketStatus.Closed => service.CloseTicketAsync(id, actor, "Test"),
        _ => throw new ArgumentOutOfRangeException(nameof(next)),
    };

    internal sealed class TestDatabase : IAsyncDisposable
    {
        private readonly DbContextOptions<ServiceDeskDbContext> _options;
        public int StoreId { get; private set; }
        public int CategoryId { get; private set; }

        private TestDatabase()
        {
            // Never migrate, clear, or drop the supplied database. Override its name first.
            var connection = new SqlConnectionStringBuilder(Environment.GetEnvironmentVariable("SERVICEDESK_TEST_SQL_CONNECTION"))
            {
                InitialCatalog = $"ServiceDesk_TransactionTests_{Guid.NewGuid():N}",
                Pooling = false,
            };
            _options = new DbContextOptionsBuilder<ServiceDeskDbContext>().UseSqlServer(connection.ConnectionString).Options;
        }

        public ServiceDeskDbContext OpenContext() => new(_options);

        public static async Task<TestDatabase> CreateAsync()
        {
            var database = new TestDatabase();
            try
            {
                await using var context = database.OpenContext();
                await context.Database.MigrateAsync();
                var store = new Store("TEST", "Test store", "Test address", "Test city");
                var category = new TicketCategory();
                context.Entry(category).Property(item => item.Name).CurrentValue = "Test category";
                context.Add(new User("Test", "Admin", "sql.admin@example.test", "000", UserRole.Admin, null));
                context.AddRange(store, category, new ExternalTechnician("Test technician", "Test company", "000", "General"));
                await context.SaveChangesAsync();
                database.StoreId = store.Id;
                database.CategoryId = category.Id;
                return database;
            }
            catch
            {
                await database.DisposeAsync();
                throw;
            }
        }

        public async Task<SupportTicket> AddTicketAsync(ServiceDeskDbContext context, TicketStatus status)
        {
            var ticket = new SupportTicket("Rollback test", "Disposable test", 1, CategoryId, StoreId, TicketPriority.High);
            if (status >= TicketStatus.Approved) ticket.Approve();
            if (status >= TicketStatus.Assigned) ticket.Assign();
            if (status >= TicketStatus.InProgress) ticket.StartProgress();
            if (status >= TicketStatus.Resolved) ticket.Resolve();
            context.SupportTickets.Add(ticket);
            await context.SaveChangesAsync();
            context.ChangeTracker.Clear();
            return ticket;
        }

        public async ValueTask DisposeAsync()
        {
            await using var context = OpenContext();
            await context.Database.EnsureDeletedAsync();
        }
    }
}

public sealed class SqlFactAttribute : FactAttribute
{
    public SqlFactAttribute()
    {
        if (!WorkflowTransactionSqlTests.SqlConfigured) Skip = "Set SERVICEDESK_TEST_SQL_CONNECTION to run isolated SQL Server tests.";
    }
}

public sealed class SqlTheoryAttribute : TheoryAttribute
{
    public SqlTheoryAttribute()
    {
        if (!WorkflowTransactionSqlTests.SqlConfigured) Skip = "Set SERVICEDESK_TEST_SQL_CONNECTION to run isolated SQL Server tests.";
    }
}
