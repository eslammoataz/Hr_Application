using Microsoft.EntityFrameworkCore;
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
public class CompanyHierarchyPositionConfiguration : IEntityTypeConfiguration<CompanyHierarchyPosition>
{
    public void Configure(EntityTypeBuilder<CompanyHierarchyPosition> builder)
    {
        builder.HasKey(ch => ch.Id);
        builder.HasQueryFilter(ch => !ch.IsDeleted);

        builder.Property(ch => ch.Role)
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.HasIndex(ch => new { ch.CompanyId, ch.Role })
            .IsUnique();

        builder.HasIndex(ch => new { ch.CompanyId, ch.SortOrder })
            .IsUnique();

        builder.HasOne(ch => ch.Company)
            .WithMany(c => c.HierarchyPositions)
            .HasForeignKey(ch => ch.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}