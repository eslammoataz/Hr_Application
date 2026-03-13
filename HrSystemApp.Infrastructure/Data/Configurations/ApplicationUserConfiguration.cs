using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        builder.Property(u => u.FcmToken)
            .HasMaxLength(500);

        builder.Property(u => u.Role)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(u => u.DeviceType)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(u => u.EmployeeId)
            .IsUnique()
            .HasFilter("\"EmployeeId\" IS NOT NULL");
    }
}