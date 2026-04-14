using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class HierarchyLevelConfiguration : IEntityTypeConfiguration<HierarchyLevel>
{
    public void Configure(EntityTypeBuilder<HierarchyLevel> builder)
    {
        builder.HasKey(l => l.Id);
        builder.HasQueryFilter(l => !l.IsDeleted);

        builder.Property(l => l.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.HasIndex(l => l.SortOrder);

        // Optional self-referencing parent for level grouping
        builder.HasOne(l => l.ParentLevel)
            .WithMany(l => l.ChildLevels)
            .HasForeignKey(l => l.ParentLevelId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}