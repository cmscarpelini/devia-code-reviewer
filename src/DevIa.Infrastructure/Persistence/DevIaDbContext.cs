using System.Reflection;
using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Audit;
using DevIa.Domain.Identity;
using DevIa.Domain.PullRequests;
using DevIa.Domain.Reviews;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;

namespace DevIa.Infrastructure.Persistence;

public sealed class DevIaDbContext(DbContextOptions<DevIaDbContext> options)
    : DbContext(options), IUnitOfWork
{
    public DbSet<User> Users => Set<User>();
    public DbSet<Organization> Organizations => Set<Organization>();
    public DbSet<CodeRepository> Repositories => Set<CodeRepository>();
    public DbSet<Membership> Memberships => Set<Membership>();
    public DbSet<PullRequest> PullRequests => Set<PullRequest>();
    public DbSet<Review> Reviews => Set<Review>();
    public DbSet<Finding> Findings => Set<Finding>();
    public DbSet<Verdict> Verdicts => Set<Verdict>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Entities generate their own Guid keys (DDD). Tell EF the app always supplies the
        // key, so new children added to an already-tracked aggregate are INSERTed (not UPDATEd).
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var key = entityType.FindPrimaryKey();
            if (key is { Properties.Count: 1 } && key.Properties[0].ClrType == typeof(Guid))
                key.Properties[0].ValueGenerated = ValueGenerated.Never;
        }

        base.OnModelCreating(modelBuilder);
    }
}
