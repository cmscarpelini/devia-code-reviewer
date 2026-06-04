using DevIa.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class CodeRepositoryConfiguration : IEntityTypeConfiguration<CodeRepository>
{
    public void Configure(EntityTypeBuilder<CodeRepository> builder)
    {
        builder.ToTable("repository");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GithubRepoId).IsRequired();
        builder.HasIndex(x => x.GithubRepoId).IsUnique();
        builder.HasIndex(x => x.OrganizationId);

        builder.Property(x => x.FullName).IsRequired().HasMaxLength(512);
        builder.Property(x => x.DefaultBranch).IsRequired().HasMaxLength(255);
        builder.Property(x => x.IsActive).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
