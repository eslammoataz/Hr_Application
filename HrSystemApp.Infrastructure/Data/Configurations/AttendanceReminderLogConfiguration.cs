using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class AttendanceReminderLogConfiguration : IEntityTypeConfiguration<AttendanceReminderLog>
{
    public void Configure(EntityTypeBuilder<AttendanceReminderLog> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => new { x.AttendanceId, x.ReminderType, x.WindowKey }).IsUnique();

        builder.Property(x => x.Channel).HasMaxLength(100);
        builder.Property(x => x.WindowKey).HasMaxLength(100);
        builder.Property(x => x.JobRunId).HasMaxLength(100);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        builder.HasOne(x => x.Attendance)
            .WithMany(a => a.ReminderLogs)
            .HasForeignKey(x => x.AttendanceId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Employee)
            .WithMany()
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
