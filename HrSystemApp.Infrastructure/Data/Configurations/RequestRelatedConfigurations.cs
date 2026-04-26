using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class RequestWorkflowConfiguration : IEntityTypeConfiguration<RequestDefinition>, IEntityTypeConfiguration<RequestWorkflowStep>
{
    public void Configure(EntityTypeBuilder<RequestDefinition> builder)
    {
        builder.ToTable("RequestDefinitions");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.RequestType)
            .WithMany()
            .HasForeignKey(x => x.RequestTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasMany(x => x.WorkflowSteps)
            .WithOne(x => x.RequestDefinition)
            .HasForeignKey(x => x.RequestDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public void Configure(EntityTypeBuilder<RequestWorkflowStep> builder)
    {
        builder.ToTable("RequestWorkflowSteps");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.StepType)
            .IsRequired();

        builder.Property(x => x.BypassHierarchyCheck)
            .HasDefaultValue(false);

        builder.Property(x => x.StartFromLevel)
            .IsRequired(false);

        builder.Property(x => x.LevelsUp)
            .IsRequired(false);

        // OrgNode FK - now optional (nullable) for DirectEmployee steps
        builder.HasOne(x => x.OrgNode)
            .WithMany()
            .HasForeignKey(x => x.OrgNodeId)
            .OnDelete(DeleteBehavior.Restrict);

        // DirectEmployee FK - optional, only for DirectEmployee steps
        builder.HasOne(x => x.DirectEmployee)
            .WithMany()
            .HasForeignKey(x => x.DirectEmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        // CompanyRole FK - optional, only for CompanyRole steps
        builder.HasOne(x => x.CompanyRole)
            .WithMany()
            .HasForeignKey(x => x.CompanyRoleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

public class RequestHistoryConfiguration : IEntityTypeConfiguration<RequestApprovalHistory>
{
    public void Configure(EntityTypeBuilder<RequestApprovalHistory> builder)
    {
        builder.ToTable("RequestApprovalHistory");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Request)
            .WithMany(x => x.ApprovalHistory)
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Approver)
            .WithMany()
            .HasForeignKey(x => x.ApproverId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
