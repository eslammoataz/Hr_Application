using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class RequestConfiguration : IEntityTypeConfiguration<Request>
{
    public void Configure(EntityTypeBuilder<Request> builder)
    {
        builder.ToTable("Requests");

        // No longer using TPH. All request types share the base Request table.
        // Specific data is stored in the Data (JSON) column.

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Details).HasMaxLength(2000);
        builder.Property(x => x.PlannedChainJson).HasMaxLength(4000);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CurrentApprover)
            .WithMany()
            .HasForeignKey(x => x.CurrentApproverId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.Property(x => x.Data).IsRequired().HasDefaultValue("{}");
    }
}

