using DevIa.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class ReviewConfiguration : IEntityTypeConfiguration<Review>
{
    public void Configure(EntityTypeBuilder<Review> builder)
    {
        builder.ToTable("review");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.HeadSha).IsRequired().HasMaxLength(64);
        builder.Property(x => x.Status).HasConversion<string>().HasMaxLength(40).IsRequired();
        builder.Property(x => x.ModelProvider).HasMaxLength(100);
        builder.Property(x => x.ModelVersion).HasMaxLength(100);
        builder.Property(x => x.RawResultRef).HasMaxLength(100);
        builder.Property(x => x.CreatedAt).IsRequired();

        // Idempotency: one review per (PR, head SHA) — SPEC-0001.
        builder.HasIndex(x => new { x.PullRequestId, x.HeadSha }).IsUnique();

        // Findings collection mapped via the private backing field.
        builder.HasMany(x => x.Findings)
            .WithOne()
            .HasForeignKey(f => f.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Findings).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Verdict: one-to-one, FK on the verdict side.
        builder.HasOne(x => x.Verdict)
            .WithOne()
            .HasForeignKey<Verdict>(v => v.ReviewId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
