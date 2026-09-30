using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Entities;


namespace ServiceDesk.Infrastructure.Configuration;

public class StoreConfiguration : IEntityTypeConfiguration<Store>
{
    public void Configure(EntityTypeBuilder<Store> builder)
    {
        builder.ToTable("Stores");
        builder.HasKey(store => store.Id);
        builder.Property(store => store.StoreCode).HasMaxLength(50).IsRequired();
        builder.Property(store => store.Name).HasMaxLength(50).IsRequired();
        builder.Property(store => store.Address).HasMaxLength(50).IsRequired();
        builder.Property(store => store.City).HasMaxLength(500).IsRequired();
        builder.Property(store => store.IsActive).IsRequired();
        builder.HasIndex(store => store.StoreCode).IsUnique();
    }
}
