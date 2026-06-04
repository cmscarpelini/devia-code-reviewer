using DevIa.Domain.Common;

namespace DevIa.Domain.Identity;

/// <summary>A GitHub organization connected to the platform.</summary>
public sealed class Organization : Entity
{
    public long GithubOrgId { get; private set; }
    public string Name { get; private set; } = null!;

    /// <summary>
    /// The GitHub App installation id for this account, captured from the installation webhook.
    /// Required to mint installation access tokens for authenticated GitHub calls (diff, checks,
    /// comments). Null until the App is installed.
    /// </summary>
    public long? InstallationId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    private Organization() { } // EF

    public Organization(long githubOrgId, string name)
    {
        if (githubOrgId <= 0) throw new DomainException("GithubOrgId must be positive.");
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Organization name is required.");

        Id = Guid.NewGuid();
        GithubOrgId = githubOrgId;
        Name = name;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Rename(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new DomainException("Organization name is required.");
        Name = name;
    }

    /// <summary>Records the GitHub App installation id (from the installation webhook).</summary>
    public void SetInstallationId(long installationId)
    {
        if (installationId <= 0) throw new DomainException("InstallationId must be positive.");
        InstallationId = installationId;
    }
}
