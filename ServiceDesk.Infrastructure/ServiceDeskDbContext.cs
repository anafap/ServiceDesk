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
        modelBuilder.ApplyConfigurationsFromAssembly(
            typeof(ServiceDeskDbContext).Assembly);
    }

}