using DevIa.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DevIa.Infrastructure.Persistence.Configurations;

public sealed class MembershipConfiguration : IEntityTypeConfiguration<Membership>
{
    public void Configure(EntityTypeBuilder<Membership> builder)
    {
        builder.ToTable("membership");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Role).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.HasIndex(x => new { x.OrganizationId, x.UserId }).IsUnique();
    }
}
