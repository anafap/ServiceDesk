using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Application.Services;
using ServiceDesk.Application.Interfaces;
using ServiceDesk.Domain.Entities;
using ServiceDesk.Domain.Enums;
using ServiceDesk.Infrastructure.Repositories;
using ServiceDesk.Infrastructure.Transactions;
namespace ServiceDesk.Tests;
public class PilotSqlTests
{
    [SqlFact]
    public async Task Profile_updates_persist_and_reject_stale_versions_without_invalidating_sessions()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var user = await db.Users.SingleAsync();
        var sessionVersion = user.SessionVersion;
        var service = new AccountService(new AccountRepository(db), new PasswordHasher<User>());
        var oldVersion = user.Version;
        await service.UpdateProfileAsync(user.Id, "Updated", "Name", "+97312345678", oldVersion);
        Assert.NotEqual(oldVersion, user.Version);
        Assert.Equal(sessionVersion, user.SessionVersion);
        await Assert.ThrowsAsync<ServiceDesk.Domain.Exceptions.StateConflictException>(() => service.UpdateProfileAsync(user.Id, "Stale", "Name", "123", oldVersion));
        await using var verify = fixture.OpenContext();
        Assert.Equal("Updated", (await verify.Users.SingleAsync()).FirstName);
    }

    [SqlFact]
    public async Task Comments_are_paged_and_stale_closure_cannot_accept_a_comment()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await fixture.AddTicketAsync(db, TicketStatus.AwaitingApproval);
        var service = new DiscussionService(new SupportTicketRepository(db), new DiscussionRepository(db), new WorkflowTransaction(db), new NotificationRepository(db));
        for (var i = 0; i < 30; i++) await service.AddAsync(ticket.Id, 1, $" Comment {i} ");
        var first = await service.GetAsync(ticket.Id, null); Assert.Equal(26, first.Count);
        var older = await service.GetAsync(ticket.Id, first[24].Id); Assert.Equal(5, older.Count);
        Assert.True(first[24].Id > older[0].Id); Assert.Empty(await db.TicketStatusHistories.ToListAsync());
        await using var other = fixture.OpenContext();
        var current = await other.SupportTickets.SingleAsync(); current.Reject(); await other.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => service.AddAsync(ticket.Id, 1, "Stale open ticket"));
        Assert.Equal(30, await db.TicketComments.CountAsync());
    }
    [SqlFact]
    public async Task Notification_failure_rolls_back_comment_and_version_and_current_scope_hides_old_notifications()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await fixture.AddTicketAsync(db, TicketStatus.AwaitingApproval);
        var manager = new User("Store", "Manager", "notify@example.test", "000", UserRole.StoreManager, fixture.StoreId);
        db.Users.Add(manager); await db.SaveChangesAsync();
        var service = new DiscussionService(new SupportTicketRepository(db), new DiscussionRepository(db), new WorkflowTransaction(db), new NotificationRepository(db));
        await service.AddAsync(ticket.Id, 1, "First comment");
        var notifications = new NotificationRepository(db);
        var first = await notifications.GetAsync(manager.Id, fixture.StoreId, null);
        Assert.Equal(1, first.UnreadCount); Assert.Single(first.Items); Assert.DoesNotContain(first.Items, n => n.RecipientId == 1);
        Assert.True(await notifications.MarkReadAsync(manager.Id, fixture.StoreId, first.Items[0].Id));
        Assert.True(await notifications.MarkReadAsync(manager.Id, fixture.StoreId, first.Items[0].Id));
        Assert.False(await notifications.MarkReadAsync(1, null, first.Items[0].Id));
        Assert.Equal(0, (await notifications.GetAsync(manager.Id, int.MaxValue, null)).UnreadCount);
        Assert.Empty((await notifications.GetAsync(manager.Id, int.MaxValue, null)).Items);
        var version = (await db.SupportTickets.SingleAsync()).Version;
        await db.Database.ExecuteSqlRawAsync("ALTER TABLE dbo.Notifications WITH NOCHECK ADD CONSTRAINT RejectComments CHECK (Kind <> 'Comment added')");
        await Assert.ThrowsAsync<DbUpdateException>(() => service.AddAsync(ticket.Id, 1, "Must roll back"));
        await using var verify = fixture.OpenContext();
        Assert.Single(await verify.TicketComments.ToListAsync());
        Assert.Equal(version, (await verify.SupportTickets.SingleAsync()).Version);
        Assert.Single(await verify.Notifications.ToListAsync());
    }
    [SqlFact]
    public async Task Sql_queries_filter_before_paging_and_password_change_uses_concurrency()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        for (var i = 0; i < 31; i++) db.SupportTickets.Add(new SupportTicket($"Printer {i}", "Description", 1, fixture.CategoryId, fixture.StoreId, TicketPriority.High));
        var otherStore = new Store("OTHER", "Other", "Address", "City"); db.Stores.Add(otherStore); await db.SaveChangesAsync();
        db.SupportTickets.Add(new SupportTicket("Printer private", "Other store", 1, fixture.CategoryId, otherStore.Id, TicketPriority.High)); await db.SaveChangesAsync();
        var repository = new TicketQueryRepository(db);
        var page = await repository.SearchAsync(fixture.StoreId, new(2, 25, " PRINTER ", TicketStatus.AwaitingApproval, TicketPriority.High));
        Assert.Equal(31, page.TotalCount); Assert.Equal(6, page.Items.Count); Assert.All(page.Items, t => Assert.Equal(fixture.StoreId, t.StoreId));
        Assert.Equal(31, (await repository.DashboardAsync(fixture.StoreId)).Summary.Total);
        Assert.Equal(5, (await repository.DashboardAsync(fixture.StoreId)).Recent.Count);
        var hasher = new PasswordHasher<User>(); var user = await db.Users.SingleAsync(); user.SetPasswordHash(hasher.HashPassword(user, "Original8!")); await db.SaveChangesAsync();
        await using var stale = fixture.OpenContext(); var staleUser = await stale.Users.SingleAsync();
        await new AccountService(new AccountRepository(db), hasher).ChangePasswordAsync(user.Id, "Original8!", "Different8!");
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => new AccountService(new AccountRepository(stale), hasher).ChangePasswordAsync(staleUser.Id, "Original8!", "Competing8!"));
    }
    [SqlFact]
    public async Task Photo_quota_claims_and_publication_are_transactional_and_lease_scoped()
    {
        var root = Path.Combine(Path.GetTempPath(), "photo-sql-" + Guid.NewGuid().ToString("N"));
        try
        {
            await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
            await using var db = fixture.OpenContext();
            var ticket = await fixture.AddTicketAsync(db, TicketStatus.AwaitingApproval);
            var storage = new ServiceDesk.Infrastructure.Photos.LocalPhotoStorage(root);
            var repository = new AttachmentRepository(db);
            var service = new AttachmentService(new SupportTicketRepository(db), repository, storage, new WorkflowTransaction(db), new NotificationRepository(db));
            var header = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };
            for (var i = 0; i < 5; i++) await service.UploadAsync(ticket.Id, 1, new MemoryStream(header), default);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.UploadAsync(ticket.Id, 1, new MemoryStream(header), default));
            Assert.Equal(5, Directory.GetFiles(Path.Combine(root, "quarantine")).Length);
            var claim = (await repository.ClaimAsync())!; Assert.Equal("processing", claim.State); Assert.Equal(1, claim.Attempts);
            await using var other = fixture.OpenContext();
            var next = await new AttachmentRepository(other).ClaimAsync(); Assert.NotEqual(claim.Id, next!.Id);
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.CompleteAsync(claim.Id, Guid.NewGuid(), "not-a-key"));
            db.ChangeTracker.Clear();
            var key = $"{claim.Id:N}-{claim.LeaseId:N}";
            await service.CompleteAsync(claim.Id, claim.LeaseId!.Value, key);
            Assert.Equal("ready", (await repository.GetAsync(claim.Id))!.State);
            Assert.Equal(key, (await repository.GetAsync(claim.Id))!.ContentKey);
            var current = await db.SupportTickets.SingleAsync(); current.Reject(); await db.SaveChangesAsync();
            await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.UploadAsync(ticket.Id, 1, new MemoryStream(header), default));
        }
        finally { if (Directory.Exists(root)) Directory.Delete(root, true); }
    }

    [SqlFact]
    public async Task Additive_migration_preserves_populated_ticket_accounts_and_password_hashes()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        await fixture.AddTicketAsync(db, TicketStatus.AwaitingApproval);
        var hasher = new PasswordHasher<User>(); var user = await db.Users.SingleAsync();
        user.SetPasswordHash(hasher.HashPassword(user, "Preserved8!")); await db.SaveChangesAsync(); var hash = user.PasswordHash;
        var migrator = Microsoft.EntityFrameworkCore.Infrastructure.AccessorExtensions.GetService<Microsoft.EntityFrameworkCore.Migrations.IMigrator>(db);
        await migrator.MigrateAsync("20261022130012_HardenAccountsAndWorkflow");
        await migrator.MigrateAsync(); db.ChangeTracker.Clear();
        Assert.Equal(hash, (await db.Users.SingleAsync()).PasswordHash);
        Assert.Single(await db.SupportTickets.ToListAsync()); Assert.Empty(await db.TicketComments.ToListAsync());
    }

    [SqlFact]
    public async Task Expired_photo_lease_is_reclaimed_and_stale_owner_cannot_publish()
    {
        await using var fixture = await WorkflowTransactionSqlTests.TestDatabase.CreateAsync();
        await using var db = fixture.OpenContext();
        var ticket = await fixture.AddTicketAsync(db, TicketStatus.AwaitingApproval);
        var item = new TicketAttachment(ticket.Id, 1); db.TicketAttachments.Add(item); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var repository = new AttachmentRepository(db); var first = (await repository.ClaimAsync())!;
        var firstLease = first.LeaseId;
        await db.TicketAttachments.Where(a => a.Id == item.Id).ExecuteUpdateAsync(set => set.SetProperty(a => a.LeaseUntil, DateTime.UtcNow.AddMinutes(-1)));
        db.ChangeTracker.Clear();
        var second = (await repository.ClaimAsync())!;
        Assert.NotEqual(firstLease, second.LeaseId); Assert.Equal(2, second.Attempts);
        var storage = new UnusedStorage();
        var service = new AttachmentService(new SupportTicketRepository(db), repository, storage, new WorkflowTransaction(db), new NotificationRepository(db));
        await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.CompleteAsync(item.Id, firstLease!.Value, "stale-key"));
        await using var verify = fixture.OpenContext();
        Assert.Null((await verify.TicketAttachments.SingleAsync()).ContentKey);
        Assert.Empty(await verify.Notifications.ToListAsync());
    }
    private sealed class UnusedStorage : IPhotoStorage
    {
        public void Cleanup(DateTime before, IReadOnlySet<string> keys) => throw new NotSupportedException();
        public void DeleteOriginal(Guid id) => throw new NotSupportedException();
        public void DeleteReady(string key) => throw new NotSupportedException();
        public Stream OpenOriginal(Guid id) => throw new NotSupportedException();
        public Stream OpenReady(string key) => throw new NotSupportedException();
        public Task PutOriginalAsync(Guid id, Stream source, CancellationToken cancellationToken) => throw new NotSupportedException();
        public Task PutReadyAsync(string key, Stream source, CancellationToken cancellationToken) => throw new NotSupportedException();
    }

}
