using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Enums;
using DevIa.Domain.Reviews;
using Microsoft.Extensions.Logging;

namespace DevIa.Application.Reviews;

/// <summary>
/// Orchestrates the review of a queued job (SPEC-0001): Pending → Processing → run the
/// pipeline → CompleteAssessment (AwaitingHumanReview). A failure marks the review Failed
/// (retryable). Idempotent: a job for a non-Pending review is skipped.
/// </summary>
public sealed class ProcessReviewJob(
    IReviewRepository reviews,
    IPullRequestRepository pullRequests,
    ICodeRepositoryRepository repositories,
    IDiffSource diffSource,
    IReviewPipeline pipeline,
    IReviewResultStore resultStore,
    IUnitOfWork unitOfWork,
    ILogger<ProcessReviewJob> logger)
{
    public async Task HandleAsync(Guid reviewId, CancellationToken cancellationToken = default)
    {
        var review = await reviews.GetByIdAsync(reviewId, cancellationToken);
        if (review is null)
        {
            logger.LogWarning("Review {ReviewId} not found; skipping.", reviewId);
            return;
        }

        if (review.Status != ReviewStatus.Pending)
        {
            logger.LogInformation("Review {ReviewId} is {Status}; skipping (idempotent).", review.Id, review.Status);
            return;
        }

        review.StartProcessing();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var pullRequest = await pullRequests.GetByIdAsync(review.PullRequestId, cancellationToken);
        var repository = pullRequest is null
            ? null
            : await repositories.GetByIdAsync(pullRequest.RepositoryId, cancellationToken);

        if (pullRequest is null || repository is null)
        {
            logger.LogError("Missing PR/repository for review {ReviewId}; failing.", review.Id);
            review.Fail();
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        try
        {
            var diff = await diffSource.GetDiffAsync(
                repository.FullName, pullRequest.GithubPrNumber, review.HeadSha, cancellationToken);

            var input = new ReviewPipelineInput(repository.FullName, pullRequest.GithubPrNumber, review.HeadSha, diff);
            var assessment = await pipeline.RunAsync(input, cancellationToken);
            var rawResultRef = await resultStore.SaveAsync(review.Id, input, assessment, cancellationToken);

            var findings = assessment.Findings.Select(f =>
                new Finding(f.Severity, f.Category, f.FilePath, f.Line, f.Title, f.Description, f.Suggestion));

            review.CompleteAssessment(
                assessment.Summary, assessment.RiskScore, assessment.ModelProvider,
                assessment.ModelVersion, assessment.TokensUsed, rawResultRef, findings);

            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Review {ReviewId} completed: {FindingCount} finding(s), awaiting human review.",
                review.Id, assessment.Findings.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Review pipeline failed for {ReviewId}; marking Failed (retryable).", review.Id);
            review.Fail();
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }
}
