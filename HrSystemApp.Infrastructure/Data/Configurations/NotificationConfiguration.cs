using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("Notifications");
        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.Title)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(x => x.Message)
            .HasMaxLength(2000)
            .IsRequired();

        builder.Property(x => x.Type)
            .HasConversion<string>()
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(x => x.IsRead)
            .HasDefaultValue(false)
            .IsRequired();

        builder.HasIndex(x => new { x.EmployeeId, x.CreatedAt });
        builder.HasIndex(x => new { x.EmployeeId, x.IsRead });

        builder.HasOne(x => x.Employee)
            .WithMany(e => e.Notifications)
            .HasForeignKey(x => x.EmployeeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
