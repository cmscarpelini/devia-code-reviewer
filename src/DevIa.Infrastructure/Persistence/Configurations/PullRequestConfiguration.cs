using DevIa.Domain.PullRequests;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class PullRequestConfiguration : IEntityTypeConfiguration<PullRequest>
{
    public void Configure(EntityTypeBuilder<PullRequest> builder)
    {
        builder.ToTable("pull_request");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GithubPrNumber).IsRequired();
        builder.HasIndex(x => new { x.RepositoryId, x.GithubPrNumber }).IsUnique();

        builder.Property(x => x.Title).IsRequired().HasMaxLength(1000);
        builder.Property(x => x.BaseBranch).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Url).IsRequired().HasMaxLength(1024);
        builder.Property(x => x.State).IsRequired().HasMaxLength(50);
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired();
    }
}
