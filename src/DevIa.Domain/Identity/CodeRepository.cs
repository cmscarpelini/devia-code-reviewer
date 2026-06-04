using DevIa.Domain.Common;

namespace DevIa.Domain.Identity;

/// <summary>
/// A source-code repository connected to the platform (maps to the <c>repository</c> table).
/// Named <c>CodeRepository</c> to avoid clashing with the persistence Repository pattern.
/// </summary>
public sealed class CodeRepository : Entity
{
    public Guid OrganizationId { get; private set; }
    public long GithubRepoId { get; private set; }
    public string FullName { get; private set; } = null!; // "org/repo"
    public string DefaultBranch { get; private set; } = null!;
    public bool IsActive { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private CodeRepository() { } // EF

    public CodeRepository(Guid organizationId, long githubRepoId, string fullName, string defaultBranch)
    {
        if (organizationId == default) throw new DomainException("OrganizationId is required.");
        if (githubRepoId <= 0) throw new DomainException("GithubRepoId must be positive.");
        if (string.IsNullOrWhiteSpace(fullName)) throw new DomainException("FullName is required.");
        if (string.IsNullOrWhiteSpace(defaultBranch)) throw new DomainException("DefaultBranch is required.");

        Id = Guid.NewGuid();
        OrganizationId = organizationId;
        GithubRepoId = githubRepoId;
        FullName = fullName;
        DefaultBranch = defaultBranch;
        IsActive = true;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    public void Activate() => IsActive = true;

    /// <summary>Stops new reviews without deleting history (SPEC-0002).</summary>
    public void Deactivate() => IsActive = false;
}
