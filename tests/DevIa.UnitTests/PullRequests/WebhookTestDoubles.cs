using DevIa.Application.Abstractions.Messaging;
using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Enums;
using DevIa.Domain.Identity;
using DevIa.Domain.PullRequests;
using DevIa.Domain.Reviews;

namespace DevIa.UnitTests.PullRequests;

internal sealed class FakeOrganizationRepository : IOrganizationRepository
{
    public readonly List<Organization> Items = [];
    public Task<Organization?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<Organization?> GetByGithubIdAsync(long githubOrgId, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.GithubOrgId == githubOrgId));
    public void Add(Organization organization) => Items.Add(organization);
}

internal sealed class FakeCodeRepositoryRepository : ICodeRepositoryRepository
{
    public readonly List<CodeRepository> Items = [];
    public Task<CodeRepository?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<CodeRepository?> GetByGithubIdAsync(long githubRepoId, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.GithubRepoId == githubRepoId));
    public Task<CodeRepository?> GetByFullNameAsync(string fullName, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.FullName == fullName));
    public Task<IReadOnlyList<CodeRepository>> ListByOrganizationAsync(Guid organizationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CodeRepository>>(Items.Where(x => x.OrganizationId == organizationId).ToList());
    public Task<IReadOnlyList<CodeRepository>> ListAllAsync(CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<CodeRepository>>(Items.ToList());
    public void Add(CodeRepository repository) => Items.Add(repository);
}

internal sealed class FakeUserRepository : IUserRepository
{
    public readonly List<User> Items = [];
    public Task<User?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<User?> GetByGithubIdAsync(long githubUserId, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.GithubUserId == githubUserId));
    public void Add(User user) => Items.Add(user);
}

internal sealed class FakePullRequestRepository : IPullRequestRepository
{
    public readonly List<PullRequest> Items = [];
    public Task<PullRequest?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<PullRequest?> GetByRepositoryAndNumberAsync(Guid repositoryId, int githubPrNumber, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.RepositoryId == repositoryId && x.GithubPrNumber == githubPrNumber));
    public void Add(PullRequest pullRequest) => Items.Add(pullRequest);
}

internal sealed class FakeReviewRepository : IReviewRepository
{
    public readonly List<Review> Items = [];
    public Task<Review?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.Id == id));
    public Task<Review?> GetByPullRequestAndShaAsync(Guid pullRequestId, string headSha, CancellationToken ct = default)
        => Task.FromResult(Items.FirstOrDefault(x => x.PullRequestId == pullRequestId && x.HeadSha == headSha));
    public Task<IReadOnlyList<Review>> ListByStatusAsync(ReviewStatus status, int page, int pageSize, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Review>>(Items.Where(x => x.Status == status).ToList());
    public void Add(Review review) => Items.Add(review);
}

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }
    public Task<int> SaveChangesAsync(CancellationToken ct = default)
    {
        SaveCount++;
        return Task.FromResult(0);
    }
}

internal sealed class FakeReviewJobQueue : IReviewJobQueue
{
    public readonly List<ReviewQueuedMessage> Messages = [];
    public Task EnqueueAsync(ReviewQueuedMessage message, CancellationToken ct = default)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }
}
