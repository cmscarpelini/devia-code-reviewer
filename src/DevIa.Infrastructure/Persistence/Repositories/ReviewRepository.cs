using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Enums;
using DevIa.Domain.Reviews;
using Microsoft.EntityFrameworkCore;

namespace DevIa.Infrastructure.Persistence.Repositories;

public sealed class ReviewRepository(DevIaDbContext db) : IReviewRepository
{
    public Task<Review?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => db.Reviews
            .Include(r => r.Findings)
            .Include(r => r.Verdict)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<Review?> GetByPullRequestAndShaAsync(Guid pullRequestId, string headSha, CancellationToken cancellationToken = default)
        => db.Reviews.FirstOrDefaultAsync(
            r => r.PullRequestId == pullRequestId && r.HeadSha == headSha,
            cancellationToken);

    public async Task<IReadOnlyList<Review>> ListByStatusAsync(ReviewStatus status, int page, int pageSize, CancellationToken cancellationToken = default)
        => await db.Reviews
            .Where(r => r.Status == status)
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

    public void Add(Review review) => db.Reviews.Add(review);
}
