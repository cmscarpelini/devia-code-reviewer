using DevIa.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class VerdictConfiguration : IEntityTypeConfiguration<Verdict>
{
    public void Configure(EntityTypeBuilder<Verdict> builder)
    {
        builder.ToTable("verdict");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(x => x.Justification);
        builder.Property(x => x.CreatedAt).IsRequired();

        // Exactly one verdict per review — SPEC-0003.
        builder.HasIndex(x => x.ReviewId).IsUnique();
    }
}
