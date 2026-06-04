using DevIa.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class FindingConfiguration : IEntityTypeConfiguration<Finding>
{
    public void Configure(EntityTypeBuilder<Finding> builder)
    {
        builder.ToTable("finding");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Severity).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Category).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.FilePath).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.Title).IsRequired().HasMaxLength(500);
        builder.Property(x => x.Description).IsRequired();
        builder.Property(x => x.Suggestion);

        builder.HasIndex(x => x.ReviewId);
        // Mirrored metadata for metrics (ADR-0003).
        builder.HasIndex(x => new { x.Severity, x.Category });
    }
}
