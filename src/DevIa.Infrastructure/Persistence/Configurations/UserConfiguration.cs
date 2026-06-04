using DevIa.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("user");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.GithubUserId).IsRequired();
        builder.HasIndex(x => x.GithubUserId).IsUnique();

        builder.Property(x => x.Login).IsRequired().HasMaxLength(255);
        builder.Property(x => x.Name).HasMaxLength(255);
        builder.Property(x => x.Email).HasMaxLength(320);
        builder.Property(x => x.AvatarUrl).HasMaxLength(1024);
        builder.Property(x => x.CreatedAt).IsRequired();
    }
}
