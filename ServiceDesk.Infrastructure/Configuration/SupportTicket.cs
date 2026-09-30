using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Infrastructure.Configuration;

public class SupportTicketsConfiguration : IEntityTypeConfiguration<SupportTicket>
{

    public void Configure(EntityTypeBuilder<SupportTicket> builder)
    {
        builder.ToTable("SupportTickets");
        builder.HasKey(SupportTicket => SupportTicket.Id);
        builder.Property(SupportTicket => SupportTicket.Title).HasMaxLength(200).IsRequired();
        builder.Property(SupportTicket => SupportTicket.Description).HasMaxLength(4000).IsRequired();
        builder.Property(SupportTicket => SupportTicket.Status).HasConversion<int>().IsRequired();
        builder.Property(SupportTicket => SupportTicket.Priority).HasConversion<int>().IsRequired();
        builder.Property(SupportTicket => SupportTicket.CreatedAt).IsRequired();
        builder.Property(SupportTicket => SupportTicket.CreatedByUserId).IsRequired();
        builder.HasOne<TicketCategory>().WithMany().HasForeignKey(SupportTicket => SupportTicket.CategoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Store>().WithMany().HasForeignKey(SupportTicket => SupportTicket.StoreId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Asset>().WithMany().HasForeignKey(SupportTicket => SupportTicket.AssetId).OnDelete(DeleteBehavior.Restrict);


    }
}

