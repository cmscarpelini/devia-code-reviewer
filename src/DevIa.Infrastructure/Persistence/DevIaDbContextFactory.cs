using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace DevIa.Infrastructure.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> to create migrations without running the
/// app. The connection string here is a placeholder — it is not used to apply migrations.
/// </summary>
public sealed class DevIaDbContextFactory : IDesignTimeDbContextFactory<DevIaDbContext>
{
    public DevIaDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<DevIaDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=devia;Username=devia;Password=devia")
            .UseSnakeCaseNamingConvention()
            .Options;

        return new DevIaDbContext(options);
    }
}
