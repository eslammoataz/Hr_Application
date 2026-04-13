using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using HrSystemApp.Domain.Models;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class EmployeeConfiguration : IEntityTypeConfiguration<Employee>
{
    public void Configure(EntityTypeBuilder<Employee> builder)
    {
        builder.HasKey(e => e.Id);
        builder.HasQueryFilter(e => !e.IsDeleted);

        // Indexes
        builder.HasIndex(e => e.EmployeeCode).IsUnique();
        builder.HasIndex(e => e.Email);
        builder.HasIndex(e => e.UserId)
            .IsUnique()
            .HasFilter("\"UserId\" IS NOT NULL");

        // Properties
        builder.Property(e => e.EmployeeCode)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.FullName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(e => e.Email)
            .HasMaxLength(256)
            .IsRequired();

        builder.Property(e => e.PhoneNumber)
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(e => e.Address)
            .HasMaxLength(500);

        builder.Property(e => e.EmploymentStatus)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(e => e.MedicalClass)
            .HasConversion<string>()
            .HasMaxLength(5);

        builder.Property(e => e.UserId)
            .HasMaxLength(450);

        builder.Property(e => e.CreatedById)
            .HasMaxLength(450);

        builder.Property(e => e.UpdatedById)
            .HasMaxLength(450);

        // Relationships
        builder.HasOne(e => e.Company)
            .WithMany(c => c.Employees)
            .HasForeignKey(e => e.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Department)
            .WithMany()
            .HasForeignKey(e => e.DepartmentId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Unit)
            .WithMany()
            .HasForeignKey(e => e.UnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Team)
            .WithMany(t => t.Members)
            .HasForeignKey(e => e.TeamId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.Manager)
            .WithMany()
            .HasForeignKey(e => e.ManagerId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.User)
            .WithOne(u => u.Employee)
            .HasForeignKey<Employee>(e => e.UserId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne(e => e.CompanyLocation)
            .WithMany()
            .HasForeignKey(e => e.CompanyLocationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}