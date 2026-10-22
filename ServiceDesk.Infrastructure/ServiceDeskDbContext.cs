using System.Data;
using Microsoft.EntityFrameworkCore;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Infrastructure;

public class ServiceDeskDbContext : DbContext
{
    public ServiceDeskDbContext(DbContextOptions<ServiceDeskDbContext> options) : base(options)
    {
    }
    public DbSet<Store> Stores => Set<Store>();
    public DbSet<Asset> Assets => Set<Asset>();
    public DbSet<User> Users => Set<User>();
    public DbSet<ExternalTechnician> ExternalTechnicians => Set<ExternalTechnician>();
    public DbSet<TicketCategory> TicketCategories => Set<TicketCategory>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<TechnicianAssignment> TechnicianAssignments => Set<TechnicianAssignment>();
    public DbSet<TicketStatusHistory> TicketStatusHistories => Set<TicketStatusHistory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        foreach (var type in new[] { typeof(SupportTicket), typeof(Store), typeof(ExternalTechnician), typeof(User) })
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