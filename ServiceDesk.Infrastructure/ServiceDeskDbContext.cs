using System.Data;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Infrastructure;

public class ServiceDeskDbContext : DbContext
{
    public ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : base(options)
    {
    }
    public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<TicketComment> TicketComments => Set<TicketComment>();
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalTechnician> ExternalTechnicians => Set<ExternalTechnician>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<VisitEvent> VisitEvents => Set<VisitEvent>();
    public DbSet<TechnicianAssignment> TechnicianAssignments => Set<TechnicianAssignment>();
    public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var type in new[] { typeof(SupportTicket), typeof(Store), typeof(ExternalTechnician), typeof(User), typeof(TicketAttachment), typeof(TechnicianAssignment) })
        {
            modelBuilder.Entity(type).Property(nameof(VersionedEntity.RowVersion)).IsRowVersion();
            modelBuilder.Entity(type).Ignore(nameof(VersionedEntity.Version));
        }
        modelBuilder.Entity<User>().Property(user => user.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<User>().Property(user => user.SessionVersion).HasDefaultValue(1);
        modelBuilder.Entity<User>().Property(user => user.NormalizedEmail).HasMaxLength(320)
            .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Email])))", stored: true);
        modelBuilder.Entity<User>().HasIndex(user => user.NormalizedEmail).IsUnique();
        modelBuilder.Entity<SupportTicket>().HasOne<User>().WithMany().HasForeignKey(ticket => ticket.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        var assignments = modelBuilder.Entity<TechnicianAssignment>();
        assignments.HasOne<SupportTicket>().WithMany().HasForeignKey(item => item.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        assignments.HasOne<ExternalTechnician>().WithMany().HasForeignKey(item => item.ExternalTechnicianId).OnDelete(DeleteBehavior.Restrict);
        assignments.HasOne<User>().WithMany().HasForeignKey(item => item.AssignedByUserId).OnDelete(DeleteBehavior.Restrict);
        assignments.HasIndex(item => new { item.SupportTicketId, item.WithdrawnAt, item.AssignedAt });
        assignments.Property(a => a.VisitState).HasMaxLength(30).HasDefaultValue("AwaitingConfirmation");
        assignments.Property(a => a.ContactName).HasMaxLength(100);
        assignments.Property(a => a.ContactPhone).HasMaxLength(50);
        assignments.Property(a => a.DeclineReason).HasMaxLength(2000);
        assignments.HasOne<User>().WithMany().HasForeignKey(a => a.ConfirmedByUserId).OnDelete(DeleteBehavior.Restrict);
        assignments.HasMany(a => a.VisitEvents).WithOne().HasForeignKey(e => e.TechnicianAssignmentId).OnDelete(DeleteBehavior.Restrict);
        assignments.Navigation(a => a.VisitEvents).UsePropertyAccessMode(PropertyAccessMode.Field);
        var visits = modelBuilder.Entity<VisitEvent>();
        visits.Property(e => e.Kind).HasMaxLength(30);
        visits.Property(e => e.ContactName).HasMaxLength(100);
        visits.Property(e => e.ContactPhone).HasMaxLength(50);
        visits.Property(e => e.Reason).HasMaxLength(2000);
        visits.HasOne<User>().WithMany().HasForeignKey(e => e.ActorId).OnDelete(DeleteBehavior.Restrict);
        visits.HasIndex(e => new { e.TechnicianAssignmentId, e.Id });
        var photos = modelBuilder.Entity<TicketAttachment>();
        photos.Property(a => a.State).HasMaxLength(20).IsRequired();
        photos.Property(a => a.ContentKey).HasMaxLength(65);
        photos.HasOne<SupportTicket>().WithMany().HasForeignKey(a => a.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        photos.HasOne<User>().WithMany().HasForeignKey(a => a.AuthorId).OnDelete(DeleteBehavior.Restrict);
        photos.HasIndex(a => new { a.SupportTicketId, a.State });
        photos.HasIndex(a => new { a.State, a.LeaseUntil, a.CreatedAt });
        var notifications = modelBuilder.Entity<Notification>();
        notifications.Property(n => n.EventKey).HasMaxLength(100).IsRequired();
        notifications.Property(n => n.Kind).HasMaxLength(100).IsRequired();
        notifications.HasOne<User>().WithMany().HasForeignKey(n => n.RecipientId).OnDelete(DeleteBehavior.Restrict);
        notifications.HasOne<SupportTicket>().WithMany().HasForeignKey(n => n.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        notifications.HasIndex(n => new { n.RecipientId, n.EventKey }).IsUnique();
        notifications.HasIndex(n => new { n.RecipientId, n.Id });
        var comments = modelBuilder.Entity<TicketComment>();
        comments.Property(c => c.Text).HasMaxLength(2000).IsRequired();
        comments.HasOne<SupportTicket>().WithMany().HasForeignKey(c => c.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        comments.HasOne<User>().WithMany().HasForeignKey(c => c.AuthorId).OnDelete(DeleteBehavior.Restrict);
        comments.HasIndex(c => new { c.SupportTicketId, c.Id });
        var histories = modelBuilder.Entity<TicketStatusHistory>();
        histories.HasOne<SupportTicket>().WithMany().HasForeignKey(item => item.SupportTicketId).OnDelete(DeleteBehavior.Restrict);
        histories.HasOne<User>().WithMany().HasForeignKey(item => item.ChangedByUserId).OnDelete(DeleteBehavior.Restrict);
        histories.HasOne<TicketStatusHistory>().WithMany().HasForeignKey(item => item.ReversesHistoryId).OnDelete(DeleteBehavior.Restrict);
        histories.HasOne<TechnicianAssignment>().WithMany().HasForeignKey(item => item.TechnicianAssignmentId).OnDelete(DeleteBehavior.Restrict);
        histories.HasIndex(item => new { item.SupportTicketId, item.Id });
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ServiceDeskDbContext).Assembly);
    }

}