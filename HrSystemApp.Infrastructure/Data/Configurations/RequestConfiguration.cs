using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests");

        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.Property(x => x.PlannedStepsJson).HasMaxLength(8000);
        builder.Property(x => x.CurrentStepApproverIds).HasMaxLength(1000);
        builder.Property(x => x.DynamicDataJson).IsRequired().HasDefaultValue("{}");
        builder.Property(x => x.CapturedSchemaJson).HasColumnType("text");
        builder.Property(x => x.RequestNumber).HasMaxLength(50);

        // Indexes
        builder.HasIndex(x => x.CurrentStepApproverIds);
        builder.HasIndex(x => x.RequestTypeId);
        builder.HasIndex(x => x.DueDate);
        builder.HasIndex(x => x.SlaBreachedAt);

        // Relationships
        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.RequestType)
            .WithMany()
            .HasForeignKey(x => x.RequestTypeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

