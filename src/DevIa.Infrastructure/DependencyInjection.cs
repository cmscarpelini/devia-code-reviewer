using DevIa.Application.Abstractions.Messaging;
using DevIa.Application.Abstractions.Persistence;
using DevIa.Application.Identity;
using DevIa.Application.Reviews;
using DevIa.Infrastructure.Auth;
using DevIa.Infrastructure.GitHub;
using DevIa.Infrastructure.Messaging;
using DevIa.Infrastructure.Persistence;
using DevIa.Infrastructure.Persistence.Queries;
using DevIa.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace DevIa.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Registers the EF Core context, the unit of work, the repositories, and the RabbitMQ
    /// messaging adapter. Reads <c>ConnectionStrings:Postgres</c> and the <c>RabbitMq</c> section.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Missing connection string 'Postgres'.");

        services.AddDbContext<DevIaDbContext>(options =>
            options.UseNpgsql(connectionString).UseSnakeCaseNamingConvention());

        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<DevIaDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IOrganizationRepository, OrganizationRepository>();
        services.AddScoped<ICodeRepositoryRepository, CodeRepositoryRepository>();
        services.AddScoped<IPullRequestRepository, PullRequestRepository>();
        services.AddScoped<IReviewRepository, ReviewRepository>();
        services.AddScoped<IAuditLogRepository, AuditLogRepository>();
        services.AddScoped<IReviewQueries, ReviewQueries>();

        // GitHub App authentication: mint installation access tokens (cached) for authenticated
        // REST calls. The named "github" client backs the token provider; the authenticator
        // resolves a repo's installation token (degrades gracefully when the App is unconfigured).
        services.Configure<GitHubAppOptions>(configuration.GetSection("GitHub"));
        services.AddHttpClient("github");
        services.AddSingleton<IGitHubInstallationTokenProvider, GitHubInstallationTokenProvider>();
        services.AddScoped<GitHubInstallationAuthenticator>();

        // Reflect the verdict on GitHub via a Check Run + Comment (SPEC-0003).
        services.AddHttpClient<IVerdictNotifier, GitHubVerdictNotifier>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DevIa-CodeReviewer"));

        // RabbitMQ messaging
        services.Configure<RabbitMqOptions>(configuration.GetSection("RabbitMq"));
        services.AddSingleton<RabbitMqConnection>();
        services.AddSingleton<IReviewJobQueue, RabbitMqReviewJobQueue>();

        // Auth (SPEC-0002): JWT issuance + GitHub OAuth code exchange.
        services.Configure<AuthOptions>(configuration.GetSection("Auth"));
        services.AddScoped<IUserTokenService, JwtUserTokenService>();
        services.AddHttpClient<IGitHubOAuthClient, GitHubOAuthClient>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DevIa-CodeReviewer"));

        return services;
    }
}
