using Microsoft.EntityFrameworkCore;
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
public class TeamConfiguration : IEntityTypeConfiguration<Team>
{
    public void Configure(EntityTypeBuilder<Team> builder)
    {
        builder.HasKey(t => t.Id);
        builder.HasQueryFilter(t => !t.IsDeleted);

        builder.HasIndex(t => new { t.UnitId, t.Name })
            .IsUnique();

        builder.Property(t => t.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(t => t.Description)
            .HasMaxLength(1000);

        builder.Property(t => t.CreatedById)
            .HasMaxLength(450);

        builder.Property(t => t.UpdatedById)
            .HasMaxLength(450);

        builder.HasOne(t => t.Unit)
            .WithMany(u => u.Teams)
            .HasForeignKey(t => t.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(t => t.TeamLeader)
            .WithMany()
            .HasForeignKey(t => t.TeamLeaderId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}