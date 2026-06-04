using DevIa.Domain.Common;

namespace DevIa.Domain.PullRequests;

/// <summary>A GitHub Pull Request; may have many reviews over time (one per relevant push).</summary>
public sealed class PullRequest : Entity
{
    public Guid RepositoryId { get; private set; }
    public int GithubPrNumber { get; private set; }
    public Guid AuthorUserId { get; private set; }
    public string Title { get; private set; } = null!;
    public string BaseBranch { get; private set; } = null!;
    public string Url { get; private set; } = null!;
    public string State { get; private set; } = null!;
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private PullRequest() { } // EF

    public PullRequest(
        Guid repositoryId,
        int githubPrNumber,
        Guid authorUserId,
        string title,
        string baseBranch,
        string url,
        string state)
    {
        if (repositoryId == default) throw new DomainException("RepositoryId is required.");
        if (githubPrNumber <= 0) throw new DomainException("GithubPrNumber must be positive.");
        if (authorUserId == default) throw new DomainException("AuthorUserId is required.");
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required.");
        if (string.IsNullOrWhiteSpace(baseBranch)) throw new DomainException("BaseBranch is required.");
        if (string.IsNullOrWhiteSpace(url)) throw new DomainException("Url is required.");
        if (string.IsNullOrWhiteSpace(state)) throw new DomainException("State is required.");

        Id = Guid.NewGuid();
        RepositoryId = repositoryId;
        GithubPrNumber = githubPrNumber;
        AuthorUserId = authorUserId;
        Title = title;
        BaseBranch = baseBranch;
        Url = url;
        State = state;
        CreatedAt = DateTimeOffset.UtcNow;
        UpdatedAt = CreatedAt;
    }

    public void UpdateDetails(string title, string state)
    {
        if (string.IsNullOrWhiteSpace(title)) throw new DomainException("Title is required.");
        if (string.IsNullOrWhiteSpace(state)) throw new DomainException("State is required.");
        Title = title;
        State = state;
        UpdatedAt = DateTimeOffset.UtcNow;
    }
}
