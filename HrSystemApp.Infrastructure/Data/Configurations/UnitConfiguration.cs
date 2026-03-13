using Microsoft.EntityFrameworkCore;
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
public class UnitConfiguration : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.HasKey(u => u.Id);
        builder.HasQueryFilter(u => !u.IsDeleted);

        builder.HasIndex(u => new { u.DepartmentId, u.Name })
            .IsUnique();

        builder.Property(u => u.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(u => u.Description)
            .HasMaxLength(1000);

        builder.Property(u => u.CreatedById)
            .HasMaxLength(450);

        builder.Property(u => u.UpdatedById)
            .HasMaxLength(450);

        builder.HasOne(u => u.Department)
            .WithMany(d => d.Units)
            .HasForeignKey(u => u.DepartmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(u => u.UnitLeader)
            .WithMany()
            .HasForeignKey(u => u.UnitLeaderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}