using DevIa.Domain.PullRequests;

namespace DevIa.Application.Abstractions.Persistence;

public interface IPullRequestRepository
{
    Task<PullRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<PullRequest?> GetByRepositoryAndNumberAsync(Guid repositoryId, int githubPrNumber, CancellationToken cancellationToken = default);

    void Add(PullRequest pullRequest);
}
