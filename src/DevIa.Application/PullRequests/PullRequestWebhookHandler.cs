using DevIa.Application.Abstractions.Messaging;
using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Identity;
using DevIa.Domain.PullRequests;
using DevIa.Domain.Reviews;
using Microsoft.Extensions.Logging;

namespace DevIa.Application.PullRequests;

/// <summary>
/// Handles a GitHub <c>pull_request</c> webhook (SPEC-0001, slice B1): upserts the
/// organization/repository/author/PR, creates a <see cref="Review"/> in <c>Pending</c>
/// idempotently by (PR, head SHA), and enqueues the review job.
/// </summary>
public sealed class PullRequestWebhookHandler(
    IOrganizationRepository organizations,
    ICodeRepositoryRepository repositories,
    IUserRepository users,
    IPullRequestRepository pullRequests,
    IReviewRepository reviews,
    IReviewJobQueue queue,
    IUnitOfWork unitOfWork,
    ILogger<PullRequestWebhookHandler> logger)
{
    private static readonly HashSet<string> HandledActions =
        new(StringComparer.OrdinalIgnoreCase) { "opened", "synchronize" };

    public async Task<WebhookProcessingResult> HandleAsync(PullRequestWebhookInput input, CancellationToken cancellationToken = default)
    {
        if (!HandledActions.Contains(input.Action))
        {
            logger.LogInformation("Ignoring pull_request action '{Action}'.", input.Action);
            return WebhookProcessingResult.Ignored();
        }

        var organization = await organizations.GetByGithubIdAsync(input.GithubOrgId, cancellationToken);
        if (organization is null)
        {
            organization = new Organization(input.GithubOrgId, input.OrgName);
            organizations.Add(organization);
        }

        var repository = await repositories.GetByGithubIdAsync(input.GithubRepoId, cancellationToken);
        if (repository is not null && !repository.IsActive)
        {
            // SPEC-0002: only active connected repositories trigger reviews.
            logger.LogInformation("Repository {Repo} is inactive; ignoring PR #{Pr}.", input.RepoFullName, input.PrNumber);
            return WebhookProcessingResult.Ignored();
        }
        if (repository is null)
        {
            repository = new CodeRepository(organization.Id, input.GithubRepoId, input.RepoFullName, input.DefaultBranch);
            repositories.Add(repository);
        }

        var author = await users.GetByGithubIdAsync(input.AuthorGithubId, cancellationToken);
        if (author is null)
        {
            author = new User(input.AuthorGithubId, input.AuthorLogin);
            users.Add(author);
        }
        else
        {
            author.UpdateProfile(input.AuthorLogin, author.Name, author.Email, author.AvatarUrl);
        }

        var pullRequest = await pullRequests.GetByRepositoryAndNumberAsync(repository.Id, input.PrNumber, cancellationToken);
        if (pullRequest is null)
        {
            pullRequest = new PullRequest(
                repository.Id, input.PrNumber, author.Id, input.Title, input.BaseBranch, input.Url, input.State);
            pullRequests.Add(pullRequest);
        }
        else
        {
            pullRequest.UpdateDetails(input.Title, input.State);
        }

        // Idempotency (SPEC-0001): one review per (PR, head SHA).
        var existing = await reviews.GetByPullRequestAndShaAsync(pullRequest.Id, input.HeadSha, cancellationToken);
        if (existing is not null)
        {
            logger.LogInformation(
                "Duplicate webhook for PR {PullRequestId} sha {HeadSha}; review {ReviewId} already exists.",
                pullRequest.Id, input.HeadSha, existing.Id);
            await unitOfWork.SaveChangesAsync(cancellationToken); // persist any PR/author updates
            return WebhookProcessingResult.Duplicate(existing.Id);
        }

        var review = new Review(pullRequest.Id, input.HeadSha);
        reviews.Add(review);

        // Persist before enqueue so the message never references a missing review.
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await queue.EnqueueAsync(new ReviewQueuedMessage(review.Id, pullRequest.Id, input.HeadSha), cancellationToken);

        logger.LogInformation("Created review {ReviewId} for PR {PullRequestId} sha {HeadSha}.",
            review.Id, pullRequest.Id, input.HeadSha);
        return WebhookProcessingResult.Accepted(review.Id);
    }
}
