using System.Text.Json;
using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Audit;
using DevIa.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DevIa.Application.Reviews;

public enum RecordVerdictStatus
{
    Recorded,
    ReviewNotFound
}

public sealed record RecordVerdictResult(RecordVerdictStatus Status, Guid? VerdictId = null, VerdictDecision? Decision = null);

/// <summary>
/// Records the human verdict on a review (SPEC-0003). The domain (<c>Review.RecordVerdict</c>)
/// enforces the rules — only from AwaitingHumanReview, once, justification required to reject.
/// Writes an audit entry, then reflects the outcome on GitHub (best-effort; the verdict is the
/// source of truth, so a reflection failure does not roll it back).
/// </summary>
public sealed class RecordVerdict(
    IReviewRepository reviews,
    IPullRequestRepository pullRequests,
    ICodeRepositoryRepository repositories,
    IAuditLogRepository auditLogs,
    IVerdictNotifier notifier,
    IUnitOfWork unitOfWork,
    ILogger<RecordVerdict> logger)
{
    public async Task<RecordVerdictResult> HandleAsync(
        Guid reviewId, Guid reviewerUserId, VerdictDecision decision, string? justification,
        bool useAiAnalysisWhenNoJustification = false,
        CancellationToken cancellationToken = default)
    {
        var review = await reviews.GetByIdAsync(reviewId, cancellationToken);
        if (review is null)
            return new RecordVerdictResult(RecordVerdictStatus.ReviewNotFound);

        // When rejecting without a written justification, the reviewer can consent to use the
        // AI analysis (summary + findings) as the rationale. It is persisted as the justification
        // and therefore also becomes the GitHub PR comment, satisfying the domain rule that a
        // rejection always carries one.
        if (decision == VerdictDecision.Rejected
            && string.IsNullOrWhiteSpace(justification)
            && useAiAnalysisWhenNoJustification)
        {
            justification = AiAnalysisJustification.Build(review);
        }

        // Enforces the SPEC-0003 rules; throws DomainException on violation.
        var verdict = review.RecordVerdict(reviewerUserId, decision, justification);

        auditLogs.Add(new AuditLog(
            actorUserId: reviewerUserId,
            action: "VerdictRecorded",
            entityType: "Review",
            entityId: review.Id,
            metadata: JsonSerializer.Serialize(new { decision = decision.ToString() })));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ReflectOnGitHubAsync(review.PullRequestId, review.HeadSha, decision, justification, cancellationToken);

        logger.LogInformation("Verdict {Decision} recorded for review {ReviewId}.", decision, review.Id);
        return new RecordVerdictResult(RecordVerdictStatus.Recorded, verdict.Id, decision);
    }

    private async Task ReflectOnGitHubAsync(
        Guid pullRequestId, string headSha, VerdictDecision decision, string? justification, CancellationToken cancellationToken)
    {
        var pullRequest = await pullRequests.GetByIdAsync(pullRequestId, cancellationToken);
        var repository = pullRequest is null
            ? null
            : await repositories.GetByIdAsync(pullRequest.RepositoryId, cancellationToken);

        if (pullRequest is null || repository is null)
            return;

        try
        {
            await notifier.PublishAsync(
                new VerdictNotification(repository.FullName, pullRequest.GithubPrNumber, headSha, decision, justification),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // The verdict is already persisted; a reflection failure is retried out-of-band, not rolled back.
            logger.LogError(ex, "Failed to reflect verdict on GitHub for review {PullRequestId}.", pullRequestId);
        }
    }
}
