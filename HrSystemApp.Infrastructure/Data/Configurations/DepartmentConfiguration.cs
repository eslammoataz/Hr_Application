using Microsoft.EntityFrameworkCore;
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
public class DepartmentConfiguration : IEntityTypeConfiguration<Department>
{
    public void Configure(EntityTypeBuilder<Department> builder)
    {
        builder.HasKey(d => d.Id);
        builder.HasQueryFilter(d => !d.IsDeleted);

        builder.HasIndex(d => new { d.CompanyId, d.Name })
            .IsUnique();

        builder.Property(d => d.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(d => d.Description)
            .HasMaxLength(1000);

        builder.Property(d => d.CreatedById)
            .HasMaxLength(450);

        builder.Property(d => d.UpdatedById)
            .HasMaxLength(450);

        builder.HasOne(d => d.Company)
            .WithMany(c => c.Departments)
            .HasForeignKey(d => d.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(d => d.VicePresident)
            .WithMany()
            .HasForeignKey(d => d.VicePresidentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(d => d.Manager)
            .WithMany()
            .HasForeignKey(d => d.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}