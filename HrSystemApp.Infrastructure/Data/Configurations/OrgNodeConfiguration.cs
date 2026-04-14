using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class OrgNodeConfiguration : IEntityTypeConfiguration<OrgNode>
{
    public void Configure(EntityTypeBuilder<OrgNode> builder)
    {
        builder.HasKey(n => n.Id);
        builder.HasQueryFilter(n => !n.IsDeleted);

        builder.Property(n => n.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Indexes
        builder.HasIndex(n => n.ParentId);
        builder.HasIndex(n => n.LevelId);

        // Unique constraint: one entity can only be linked to ONE node
        // Using a filtered index for PostgreSQL (allows multiple NULLs, prevents duplicate links)
        builder.HasIndex(n => new { n.EntityId, n.EntityType })
            .IsUnique()
            .HasFilter("\"EntityId\" IS NOT NULL");

        // Self-referencing parent (Restrict to prevent circular cascade delete)
        builder.HasOne(n => n.Parent)
            .WithMany(n => n.Children)
            .HasForeignKey(n => n.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional HierarchyLevel
        builder.HasOne(n => n.Level)
            .WithMany(l => l.Nodes)
            .HasForeignKey(n => n.LevelId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}