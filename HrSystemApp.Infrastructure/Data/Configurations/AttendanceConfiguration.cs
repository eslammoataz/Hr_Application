using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class AttendanceConfiguration : IEntityTypeConfiguration<Attendance>
{
    public void Configure(EntityTypeBuilder<Attendance> builder)
    {
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.HasIndex(x => new { x.EmployeeId, x.Date }).IsUnique();
        builder.HasIndex(x => new { x.Date, x.LastClockOutUtc });

        builder.Property(x => x.TotalHours).HasPrecision(10, 2);
        builder.Property(x => x.Reason).HasMaxLength(500);
        builder.Property(x => x.Xmin)
            .HasColumnName("xmin")
            .ValueGeneratedOnAddOrUpdate()
            .IsConcurrencyToken();

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.Attendances)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.FirstClockInLog)
            .WithMany()
            .HasForeignKey(x => x.FirstClockInLogId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.LastClockOutLog)
            .WithMany()
            .HasForeignKey(x => x.LastClockOutLogId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
