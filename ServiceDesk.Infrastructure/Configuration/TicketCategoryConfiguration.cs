using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Infrastructure.Configuration;

public class TicketCategoryConfiguration : IEntityTypeConfiguration<TicketCategory>
{
    public void Configure(EntityTypeBuilder<TicketCategory> builder)
    {
        builder.ToTable("TicketCategories");

        builder.HasKey(TicketCategory => TicketCategory.Id);
        builder.Property(TicketCategory => TicketCategory.Name).HasMaxLength(100).IsRequired();


    }
}
