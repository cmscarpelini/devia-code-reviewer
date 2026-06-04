using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace DevIa.Application.Identity;

public sealed record InstallationRepositoryInput(long GithubRepoId, string FullName);

/// <summary>
/// Provider-neutral input for GitHub <c>installation</c> / <c>installation_repositories</c>
/// webhooks. <c>Repositories</c> are the affected repos: activated for created/added,
/// deactivated for deleted/removed.
/// </summary>
public sealed record InstallationWebhookInput(
    string Action,
    long InstallationId,
    long AccountGithubId,
    string AccountLogin,
    IReadOnlyList<InstallationRepositoryInput> Repositories);

/// <summary>
/// Handles a GitHub App installation event (SPEC-0002): upserts the <see cref="Organization"/>
/// and connects/disconnects its repositories. The default branch is not in the payload, so new
/// repositories default to "main" until refreshed.
/// </summary>
public sealed class ProcessInstallation(
    IOrganizationRepository organizations,
    ICodeRepositoryRepository repositories,
    IUnitOfWork unitOfWork,
    ILogger<ProcessInstallation> logger)
{
    private static readonly HashSet<string> ActivateActions =
        new(StringComparer.OrdinalIgnoreCase) { "created", "added" };

    public async Task<int> HandleAsync(InstallationWebhookInput input, CancellationToken cancellationToken = default)
    {
        var organization = await organizations.GetByGithubIdAsync(input.AccountGithubId, cancellationToken);
        if (organization is null)
        {
            organization = new Organization(input.AccountGithubId, input.AccountLogin);
            organizations.Add(organization);
        }

        // Capture the installation id so authenticated GitHub calls can mint installation tokens.
        if (input.InstallationId > 0)
            organization.SetInstallationId(input.InstallationId);

        var activate = ActivateActions.Contains(input.Action);
        var affected = 0;

        foreach (var entry in input.Repositories)
        {
            var repository = await repositories.GetByGithubIdAsync(entry.GithubRepoId, cancellationToken);
            if (activate)
            {
                if (repository is null)
                    repositories.Add(new CodeRepository(organization.Id, entry.GithubRepoId, entry.FullName, "main"));
                else
                    repository.Activate();
            }
            else
            {
                repository?.Deactivate();
            }
            affected++;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        logger.LogInformation(
            "Installation '{Action}' for {Org}: {Count} repository(ies) affected.", input.Action, input.AccountLogin, affected);
        return affected;
    }
}
