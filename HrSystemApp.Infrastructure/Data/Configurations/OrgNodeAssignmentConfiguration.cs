using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class OrgNodeAssignmentConfiguration : IEntityTypeConfiguration<OrgNodeAssignment>
{
    public void Configure(EntityTypeBuilder<OrgNodeAssignment> builder)
    {
        builder.HasKey(a => a.Id);
        builder.HasQueryFilter(a => !a.IsDeleted);

        // Unique: same employee can only be assigned once per node
        builder.HasIndex(a => new { a.OrgNodeId, a.EmployeeId }).IsUnique();

        // OrgNode → cascade delete (assignment table, OK to cascade)
        builder.HasOne(a => a.OrgNode)
            .WithMany(n => n.Assignments)
            .HasForeignKey(a => a.OrgNodeId)
            .OnDelete(DeleteBehavior.Cascade);

        // Employee → restrict (never delete employee due to assignment)
        builder.HasOne(a => a.Employee)
            .WithMany()
            .HasForeignKey(a => a.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}