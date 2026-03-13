using Microsoft.EntityFrameworkCore;
using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class CompanyConfiguration : IEntityTypeConfiguration<Company>
{
    public void Configure(EntityTypeBuilder<Company> builder)
    {
        builder.HasKey(c => c.Id);
        builder.HasQueryFilter(c => !c.IsDeleted);

        builder.Property(c => c.CompanyName)
            .HasMaxLength(300)
            .IsRequired();

        builder.Property(c => c.CompanyLogoUrl)
            .HasMaxLength(500);

        builder.Property(c => c.CreatedById)
            .HasMaxLength(450);

        builder.Property(c => c.UpdatedById)
            .HasMaxLength(450);
    }
}