using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace DevIa.Infrastructure.Persistence.Queries;

/// <summary>EF Core read model for the dashboard (joins reviews with PR/repo/author).</summary>
public sealed class ReviewQueries(DevIaDbContext db) : IReviewQueries
{
    public async Task<IReadOnlyList<ReviewListItem>> ListByStatusAsync(
        ReviewStatus status, int page, int pageSize, CancellationToken cancellationToken = default)
    {
        var query =
            from r in db.Reviews.AsNoTracking()
            where r.Status == status
            join pr in db.PullRequests.AsNoTracking() on r.PullRequestId equals pr.Id
            join repo in db.Repositories.AsNoTracking() on pr.RepositoryId equals repo.Id
            join author in db.Users.AsNoTracking() on pr.AuthorUserId equals author.Id
            orderby r.CreatedAt descending
            select new ReviewListItem(
                r.Id, repo.FullName, pr.GithubPrNumber, pr.Title, author.Login, r.HeadSha,
                r.Status, r.RiskScore, r.Findings.Count, r.CreatedAt, pr.Url);

        return await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);
    }

    public async Task<ReviewDetail?> GetDetailAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await db.Reviews.AsNoTracking()
            .Include(r => r.Findings)
            .Include(r => r.Verdict)
            .FirstOrDefaultAsync(r => r.Id == reviewId, cancellationToken);
        if (review is null)
            return null;

        var pullRequest = await db.PullRequests.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == review.PullRequestId, cancellationToken);
        var repository = pullRequest is null ? null : await db.Repositories.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == pullRequest.RepositoryId, cancellationToken);
        var author = pullRequest is null ? null : await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == pullRequest.AuthorUserId, cancellationToken);

        var findings = review.Findings
            .OrderBy(f => SeverityRank(f.Severity))
            .Select(f => new ReviewFindingDto(f.Severity, f.Category, f.FilePath, f.Line, f.Title, f.Description, f.Suggestion))
            .ToList();

        var verdict = review.Verdict is null
            ? null
            : new ReviewVerdictDto(review.Verdict.Decision, review.Verdict.Justification, review.Verdict.CreatedAt);

        return new ReviewDetail(
            review.Id,
            repository?.FullName ?? string.Empty,
            pullRequest?.GithubPrNumber ?? 0,
            pullRequest?.Title ?? string.Empty,
            author?.Login ?? string.Empty,
            review.HeadSha,
            review.Status,
            review.Summary,
            review.RiskScore,
            pullRequest?.Url ?? string.Empty,
            review.CreatedAt,
            findings,
            verdict);
    }

    private static int SeverityRank(Severity severity) => severity switch
    {
        Severity.Blocker => 0,
        Severity.Major => 1,
        Severity.Minor => 2,
        _ => 3
    };
}
