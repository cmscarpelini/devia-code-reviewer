using DevIa.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class OrganizationConfiguration : IEntityTypeConfiguration<Organization>
{
    public void Configure(EntityTypeBuilder<Organization> builder)
    {
        builder.ToTable("organization");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GithubOrgId).IsRequired();
        builder.HasIndex(x => x.GithubOrgId).IsUnique();

        builder.Property(x => x.Name).IsRequired().HasMaxLength(255);
        builder.Property(x => x.InstallationId);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
