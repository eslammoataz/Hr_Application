using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class EmployeeCompanyRoleConfiguration : IEntityTypeConfiguration<EmployeeCompanyRole>
{
    public void Configure(EntityTypeBuilder<EmployeeCompanyRole> builder)
    {
        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.EmployeeId, x.RoleId }).IsUnique();

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.CompanyRoles)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
