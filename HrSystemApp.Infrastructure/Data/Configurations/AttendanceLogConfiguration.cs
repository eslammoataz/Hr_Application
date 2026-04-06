using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class AttendanceLogConfiguration : IEntityTypeConfiguration<AttendanceLog>
{
    public void Configure(EntityTypeBuilder<AttendanceLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => new { x.EmployeeId, x.TimestampUtc });
        builder.HasIndex(x => x.IdempotencyKey).IsUnique();

        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.IdempotencyKey).HasMaxLength(200);

        builder.HasOne(x => x.Attendance)
            .WithMany(a => a.Logs)
            .HasForeignKey(x => x.AttendanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.AttendanceLogs)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
