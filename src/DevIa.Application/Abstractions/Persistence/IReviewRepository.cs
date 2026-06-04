using DevIa.Domain.Enums;
using DevIa.Domain.Reviews;

namespace DevIa.Application.Abstractions.Persistence;

public interface IReviewRepository
{
    /// <summary>Loads a review with its findings and verdict.</summary>
    Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>Idempotency check for a PR version (SPEC-0001): (PR, head SHA).</summary>
    Task<Review?> GetByPullRequestAndShaAsync(Guid pullRequestId, string headSha, CancellationToken cancellationToken = default);

    /// <summary>Paged queue for the dashboard (SPEC-0004).</summary>
    Task<IReadOnlyList<Review>> ListByStatusAsync(ReviewStatus status, int page, int pageSize, CancellationToken cancellationToken = default);

    void Add(Review review);
}
