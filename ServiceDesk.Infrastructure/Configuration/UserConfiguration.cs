using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Entities;
namespace ServiceDesk.Infrastructure.Configuration;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");

        builder.HasKey(user => user.Id);

        builder.Property(user => user.FirstName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.LastName)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(user => user.Email)
            .HasMaxLength(320)
            .IsRequired();

        builder.Property(user => user.PhoneNumber)
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(user => user.Role)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(user => user.Email)
            .IsUnique();

        builder.HasOne<Store>()
            .WithMany()
            .HasForeignKey(user => user.StoreId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.Ignore(user => user.FullName);

        builder.Property(user => user.PasswordHash).HasMaxLength(512).IsRequired(false);


    }
}
