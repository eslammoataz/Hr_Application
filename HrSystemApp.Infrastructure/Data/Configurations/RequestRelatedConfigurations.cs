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

        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);

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

public class RequestAttachmentConfiguration : IEntityTypeConfiguration<RequestAttachment>
{
    public void Configure(EntityTypeBuilder<RequestAttachment> builder)
    {
        builder.ToTable("RequestAttachments");
        builder.HasKey(x => x.Id);

        builder.HasOne(x => x.Request)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
