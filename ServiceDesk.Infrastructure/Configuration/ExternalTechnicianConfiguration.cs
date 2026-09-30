using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using ServiceDesk.Domain.Entities;

namespace ServiceDesk.Infrastructure.Configuration;

public class ExternalTechnicianConfiguration : IEntityTypeConfiguration<ExternalTechnician>
{
    public void Configure(EntityTypeBuilder<ExternalTechnician> builder)
    {
        builder.ToTable("ExternalTechnicians");

        builder.HasKey(ExternalTechnician => ExternalTechnician.Id);

        builder.Property(ExternalTechnician => ExternalTechnician.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ExternalTechnician => ExternalTechnician.CompanyName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ExternalTechnician => ExternalTechnician.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(ExternalTechnician => ExternalTechnician.Specialisation)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(ExternalTechnician => ExternalTechnician.IsActive)
           .IsRequired();
    }
}
