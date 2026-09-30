using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Infrastructure.Configuration;

public class AssetConfiguration : IEntityTypeConfiguration<Asset>
{

    public void Configure(EntityTypeBuilder<Asset> builder)
    {
        builder.ToTable("Assets");
        builder.HasKey(asset => asset.Id);
        builder.Property(asset => asset.AssetTag).HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.Name).HasMaxLength(200).IsRequired();
        builder.Property(asset => asset.SerialNumber).HasMaxLength(150).IsRequired();
        builder.Property(asset => asset.Type).HasMaxLength(100).IsRequired();
        builder.Property(asset => asset.IsActive).IsRequired();
        builder.HasIndex(asset => asset.AssetTag).IsUnique();
        builder.HasOne<Store>().WithMany().HasForeignKey(asset => asset.StoreId).OnDelete(DeleteBehavior.Restrict);
    }
}

