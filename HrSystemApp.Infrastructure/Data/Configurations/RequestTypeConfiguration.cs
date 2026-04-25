using HrSystemApp.Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace HrSystemApp.Infrastructure.Data.Configurations;

public class RequestTypeConfiguration : IEntityTypeConfiguration<RequestType>
{
    public void Configure(EntityTypeBuilder<RequestType> builder)
    {
        builder.ToTable("RequestTypes");

        builder.HasKey(x => x.Id);
        builder.HasQueryFilter(x => !x.IsDeleted);

        builder.Property(x => x.KeyName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.FormSchemaJson).HasColumnType("text");
        builder.Property(x => x.RequestNumberPattern).HasMaxLength(100);
        builder.Property(x => x.DisplayNameLocalizationsJson).HasColumnType("text");

        // Unique constraint on KeyName + CompanyId (allows null CompanyId for system types)
        builder.HasIndex(x => new { x.KeyName, x.CompanyId }).IsUnique();

        // Index for looking up by company
        builder.HasIndex(x => x.CompanyId);

        // Relationships
        builder.HasOne(x => x.Company)
            .WithMany()
            .HasForeignKey(x => x.CompanyId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
