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

        builder.Property(n => n.Type)
            .HasMaxLength(50)
            .IsRequired(false);

        builder.Property(n => n.CompanyId)
            .IsRequired();

        // Indexes
        builder.HasIndex(n => n.ParentId);
        builder.HasIndex(n => n.CompanyId);

        // Self-referencing parent (Restrict to prevent circular cascade delete)
        builder.HasOne(n => n.Parent)
            .WithMany(n => n.Children)
            .HasForeignKey(n => n.ParentId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
