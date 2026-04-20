using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class CompanyRolePermissionConfiguration : IEntityTypeConfiguration<CompanyRolePermission>
{
    public void Configure(EntityTypeBuilder<CompanyRolePermission> builder)
    {
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Permission)
            .IsRequired()
            .HasMaxLength(100);

        builder.HasIndex(x => new { x.RoleId, x.Permission }).IsUnique();
    }
}
