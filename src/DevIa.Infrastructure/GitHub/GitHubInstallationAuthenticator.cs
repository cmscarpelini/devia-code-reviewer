using DevIa.Application.Abstractions.Persistence;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DevIa.Infrastructure.GitHub;

/// <summary>
/// Resolves an installation access token for a repository (by full name): looks up the repo, its
/// organization, and the org's installation id, then asks the token provider. Returns <c>null</c>
/// when the App is not configured or the repo/installation is unknown, letting callers degrade
/// gracefully instead of failing.
/// </summary>
public sealed class GitHubInstallationAuthenticator(
    ICodeRepositoryRepository repositories,
    IOrganizationRepository organizations,
    IGitHubInstallationTokenProvider tokenProvider,
    IOptions<GitHubAppOptions> options,
    ILogger<GitHubInstallationAuthenticator> logger)
{
    private readonly GitHubAppOptions _options = options.Value;

    public async Task<string?> GetTokenForRepoAsync(string repoFullName, CancellationToken cancellationToken = default)
    {
        if (!_options.IsConfigured)
            return null;

        var repository = await repositories.GetByFullNameAsync(repoFullName, cancellationToken);
        if (repository is null)
        {
            logger.LogWarning("No connected repository '{Repo}'; cannot authenticate to GitHub.", repoFullName);
            return null;
        }

        var organization = await organizations.GetByIdAsync(repository.OrganizationId, cancellationToken);
        if (organization?.InstallationId is not { } installationId)
        {
            logger.LogWarning("Organization for '{Repo}' has no installation id; cannot authenticate.", repoFullName);
            return null;
        }

        return await tokenProvider.GetInstallationTokenAsync(installationId, cancellationToken);
    }
}
