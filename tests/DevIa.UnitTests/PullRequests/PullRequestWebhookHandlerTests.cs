using DevIa.Application.PullRequests;
using DevIa.Domain.Enums;
using DevIa.Domain.Identity;
using Microsoft.Extensions.Logging.Abstractions;

namespace DevIa.UnitTests.PullRequests;

public class PullRequestWebhookHandlerTests
{
    private sealed class Harness
    {
        public readonly FakeOrganizationRepository Orgs = new();
        public readonly FakeCodeRepositoryRepository Repos = new();
        public readonly FakeUserRepository Users = new();
        public readonly FakePullRequestRepository Prs = new();
        public readonly FakeReviewRepository Reviews = new();
        public readonly FakeReviewJobQueue Queue = new();
        public readonly FakeUnitOfWork Uow = new();

        public PullRequestWebhookHandler Handler => new(
            Orgs, Repos, Users, Prs, Reviews, Queue, Uow,
            NullLogger<PullRequestWebhookHandler>.Instance);
    }

    private static PullRequestWebhookInput Input(string sha, string action = "opened") => new(
        Action: action,
        GithubOrgId: 100, OrgName: "acme",
        GithubRepoId: 200, RepoFullName: "acme/app", DefaultBranch: "main",
        PrNumber: 7, Title: "Add feature", BaseBranch: "main",
        Url: "https://github.com/acme/app/pull/7", State: "open",
        HeadSha: sha, AuthorGithubId: 300, AuthorLogin: "dev");

    [Fact]
    public async Task First_webhook_creates_pending_review_and_enqueues()
    {
        var ctx = new Harness();

        var result = await ctx.Handler.HandleAsync(Input("sha1"));

        Assert.Equal(WebhookProcessingStatus.Accepted, result.Status);
        var review = Assert.Single(ctx.Reviews.Items);
        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.Equal("sha1", review.HeadSha);

        var message = Assert.Single(ctx.Queue.Messages);
        Assert.Equal(review.Id, message.ReviewId);
    }

    [Fact]
    public async Task First_webhook_upserts_org_repo_user_and_pr()
    {
        var ctx = new Harness();

        await ctx.Handler.HandleAsync(Input("sha1"));

        Assert.Single(ctx.Orgs.Items);
        Assert.Single(ctx.Repos.Items);
        Assert.Single(ctx.Users.Items);
        Assert.Single(ctx.Prs.Items);
    }

    [Fact]
    public async Task Duplicate_webhook_same_sha_does_not_create_or_enqueue_again()
    {
        var ctx = new Harness();
        await ctx.Handler.HandleAsync(Input("sha1"));

        var second = await ctx.Handler.HandleAsync(Input("sha1"));

        Assert.Equal(WebhookProcessingStatus.Duplicate, second.Status);
        Assert.Single(ctx.Reviews.Items);   // still one review
        Assert.Single(ctx.Queue.Messages);  // not enqueued twice
    }

    [Fact]
    public async Task New_sha_on_same_pr_creates_a_second_review_but_reuses_entities()
    {
        var ctx = new Harness();
        await ctx.Handler.HandleAsync(Input("sha1"));

        var result = await ctx.Handler.HandleAsync(Input("sha2", action: "synchronize"));

        Assert.Equal(WebhookProcessingStatus.Accepted, result.Status);
        Assert.Equal(2, ctx.Reviews.Items.Count);
        Assert.Equal(2, ctx.Queue.Messages.Count);
        // No duplicate identity rows.
        Assert.Single(ctx.Orgs.Items);
        Assert.Single(ctx.Repos.Items);
        Assert.Single(ctx.Users.Items);
        Assert.Single(ctx.Prs.Items);
    }

    [Fact]
    public async Task Inactive_repository_is_ignored()
    {
        var ctx = new Harness();
        var repository = new CodeRepository(Guid.NewGuid(), 200, "acme/app", "main"); // GithubRepoId matches Input
        repository.Deactivate();
        ctx.Repos.Add(repository);

        var result = await ctx.Handler.HandleAsync(Input("sha1"));

        Assert.Equal(WebhookProcessingStatus.Ignored, result.Status);
        Assert.Empty(ctx.Reviews.Items);
        Assert.Empty(ctx.Queue.Messages);
    }

    [Fact]
    public async Task Unhandled_action_is_ignored()
    {
        var ctx = new Harness();

        var result = await ctx.Handler.HandleAsync(Input("sha1", action: "closed"));

        Assert.Equal(WebhookProcessingStatus.Ignored, result.Status);
        Assert.Empty(ctx.Reviews.Items);
        Assert.Empty(ctx.Queue.Messages);
    }
}
