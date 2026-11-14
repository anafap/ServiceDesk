using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure;

namespace ServiceDesk.Tests;

public class VisitMigrationSqlTests
{
    [SqlFact]
    public async Task Visit_migration_preserves_populated_previous_schema_after_backup_and_restore()
    {
        await using var source = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = source.OpenContext();
        foreach (var status in new[] { TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Resolved })
        {
            var ticket = await source.AddTicketAsync(db, status);
            db.TechnicianAssignments.Add(new TechnicianAssignment(ticket.Id, 1, 1, DateTime.UtcNow.AddDays(1), "Preserved visit"));
            await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        }
        await db.GetService<IMigrator>().MigrateAsync("20261112083231_AddPilotCommunication");
        var original = new SqlConnectionStringBuilder(db.Database.GetConnectionString());
        var name = "ServiceDesk_TransactionTests_" + Guid.NewGuid().ToString("N");
        var backup = "/var/opt/mssql/data/" + name + ".bak";
        var adminSettings = new SqlConnectionStringBuilder(original.ConnectionString) { InitialCatalog = "master", Pooling = false };
        await using var admin = new SqlConnection(adminSettings.ConnectionString);
        await admin.OpenAsync();
        var files = new List<(string Name, string Kind)>();
        await using (var command = new SqlCommand("SELECT name, type_desc FROM sys.master_files WHERE database_id = DB_ID(@name)", admin))
        {
            command.Parameters.AddWithValue("@name", original.InitialCatalog);
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) files.Add((reader.GetString(0), reader.GetString(1)));
        }
        var restoredSettings = new SqlConnectionStringBuilder(original.ConnectionString) { InitialCatalog = name, Pooling = false };
        await using var restored = new ServiceDeskDbContext(new DbContextOptionsBuilder<ServiceDeskDbContext>().UseSqlServer(restoredSettings.ConnectionString).Options);
        try
        {
            await using (var backupCommand = new SqlCommand($"BACKUP DATABASE [{original.InitialCatalog}] TO DISK = @path WITH COPY_ONLY, CHECKSUM, INIT", admin) { CommandTimeout = 120 })
            {
                backupCommand.Parameters.AddWithValue("@path", backup); await backupCommand.ExecuteNonQueryAsync();
            }
            var moves = string.Join(", ", files.Select((file, index) => $"MOVE N'{file.Name.Replace("'", "''")}' TO N'/var/opt/mssql/data/{name}_{index}{(file.Kind == "LOG" ? ".ldf" : ".mdf")}'"));
            await using (var restoreCommand = new SqlCommand($"RESTORE DATABASE [{name}] FROM DISK = @path WITH {moves}, RECOVERY", admin) { CommandTimeout = 120 })
            {
                restoreCommand.Parameters.AddWithValue("@path", backup); await restoreCommand.ExecuteNonQueryAsync();
            }
            await restored.Database.MigrateAsync();
            Assert.Equal(3, await restored.SupportTickets.CountAsync());
            Assert.Equal(new[] { TicketStatus.Assigned, TicketStatus.InProgress, TicketStatus.Resolved }, await restored.SupportTickets.OrderBy(t => t.Id).Select(t => t.Status).ToArrayAsync());
            Assert.Equal(1, await restored.Users.CountAsync());
            Assert.All(await restored.TechnicianAssignments.ToListAsync(), a =>
            {
                Assert.Equal("Preserved visit", a.Notes);
                Assert.Equal("AwaitingConfirmation", a.VisitState);
                Assert.Null(a.ConfirmedAt);
                Assert.Null(a.ContactName);
                Assert.Equal(8, a.RowVersion.Length);
            });
            Assert.Empty(await restored.VisitEvents.ToListAsync());
            Assert.Equal("20261113113214_AddVisitConfirmation", (await restored.Database.GetAppliedMigrationsAsync()).Last());
        }
        finally
        {
            await restored.Database.EnsureDeletedAsync();
            // Delete only this test's backup using the server's maintained cleanup procedure.
            await using var cleanup = new SqlCommand("EXEC master.dbo.xp_delete_files @path", admin);
            cleanup.Parameters.AddWithValue("@path", backup);
            await cleanup.ExecuteNonQueryAsync();
        }
    }
}
