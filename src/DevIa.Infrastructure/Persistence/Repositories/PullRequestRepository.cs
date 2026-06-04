using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.PullRequests;
using Microsoft.EntityFrameworkCore;

namespace DevIa.Infrastructure.Persistence.Repositories;

public sealed class PullRequestRepository(DevIaDbContext db) : IPullRequestRepository
{
    public Task<PullRequest?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.PullRequests.FirstOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<PullRequest?> GetByRepositoryAndNumberAsync(Guid repositoryId, int githubPrNumber, CancellationToken cancellationToken = default)
        => db.PullRequests.FirstOrDefaultAsync(
            x => x.RepositoryId == repositoryId && x.GithubPrNumber == githubPrNumber,
            cancellationToken);

    public void Add(PullRequest pullRequest) => db.PullRequests.Add(pullRequest);
}
