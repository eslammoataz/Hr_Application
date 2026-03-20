using Microsoft.EntityFrameworkCore;
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class CompanyLocationConfiguration : IEntityTypeConfiguration<CompanyLocation>
{
    public void Configure(EntityTypeBuilder<CompanyLocation> builder)
    {
        builder.HasKey(cl => cl.Id);
        builder.HasQueryFilter(cl => !cl.IsDeleted);

        builder.Property(cl => cl.LocationName)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(cl => cl.Address)
            .HasMaxLength(500);

        builder.HasOne(cl => cl.Company)
            .WithMany(c => c.Locations)
            .HasForeignKey(cl => cl.CompanyId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}