using DevIa.Domain.Enums;

namespace DevIa.Application.Reviews;

/// <summary>A row in the review queue (SPEC-0004 dashboard).</summary>
public sealed record ReviewListItem(
    Guid Id,
    string RepositoryFullName,
    int PrNumber,
    string PullRequestTitle,
    string AuthorLogin,
    string HeadSha,
    ReviewStatus Status,
    int? RiskScore,
    int FindingCount,
    DateTimeOffset CreatedAt,
    string PrUrl);

public sealed record ReviewFindingDto(
    Severity Severity,
    FindingCategory Category,
    string FilePath,
    int? Line,
    string Title,
    string Description,
    string? Suggestion);

public sealed record ReviewVerdictDto(VerdictDecision Decision, string? Justification, DateTimeOffset CreatedAt);

/// <summary>The full assessment shown on the review detail page (SPEC-0004).</summary>
public sealed record ReviewDetail(
    Guid Id,
    string RepositoryFullName,
    int PrNumber,
    string PullRequestTitle,
    string AuthorLogin,
    string HeadSha,
    ReviewStatus Status,
    string? Summary,
    int? RiskScore,
    string PrUrl,
    DateTimeOffset CreatedAt,
    IReadOnlyList<ReviewFindingDto> Findings,
    ReviewVerdictDto? Verdict);

/// <summary>Read-side queries for the dashboard (CQRS read model).</summary>
public interface IReviewQueries
{
    Task<IReadOnlyList<ReviewListItem>> ListByStatusAsync(ReviewStatus status, int page, int pageSize, CancellationToken cancellationToken = default);

    Task<ReviewDetail?> GetDetailAsync(Guid reviewId, CancellationToken cancellationToken = default);
}
