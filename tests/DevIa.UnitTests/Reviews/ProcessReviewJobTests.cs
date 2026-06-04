using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using DevIa.Domain.Identity;
using DevIa.Domain.PullRequests;
using DevIa.Domain.Reviews;
using DevIa.UnitTests.PullRequests; // reuse the repository fakes
using Microsoft.Extensions.Logging.Abstractions;

namespace DevIa.UnitTests.Reviews;

public class ProcessReviewJobTests
{
    private sealed class FakeDiffSource : IDiffSource
    {
        public Task<string> GetDiffAsync(string repoFullName, int prNumber, string headSha, CancellationToken ct = default)
            => Task.FromResult("diff text");
    }

    private sealed class StubReviewPipeline(ReviewAssessment assessment) : IReviewPipeline
    {
        public Task<ReviewAssessment> RunAsync(ReviewPipelineInput input, CancellationToken ct = default)
            => Task.FromResult(assessment);
    }

    private sealed class ThrowingReviewPipeline : IReviewPipeline
    {
        public Task<ReviewAssessment> RunAsync(ReviewPipelineInput input, CancellationToken ct = default)
            => throw new InvalidOperationException("llm down");
    }

    private sealed class StubReviewResultStore : IReviewResultStore
    {
        public Task<string> SaveAsync(Guid reviewId, ReviewPipelineInput input, ReviewAssessment assessment, CancellationToken ct = default)
            => Task.FromResult("raw-ref-1");
    }

    private sealed class Harness
    {
        public readonly FakeReviewRepository Reviews = new();
        public readonly FakePullRequestRepository Prs = new();
        public readonly FakeCodeRepositoryRepository Repos = new();
        public readonly FakeUnitOfWork Uow = new();
        public Review Review = default!;

        public Harness Seed()
        {
            var repository = new CodeRepository(Guid.NewGuid(), 200, "acme/app", "main");
            var pullRequest = new PullRequest(repository.Id, 7, Guid.NewGuid(), "title", "main", "https://x/pull/7", "open");
            Review = new Review(pullRequest.Id, "sha1");

            Repos.Add(repository);
            Prs.Add(pullRequest);
            Reviews.Add(Review);
            return this;
        }

        public ProcessReviewJob Handler(IReviewPipeline pipeline) => new(
            Reviews, Prs, Repos, new FakeDiffSource(), pipeline, new StubReviewResultStore(), Uow,
            NullLogger<ProcessReviewJob>.Instance);
    }

    private static ReviewAssessment Assessment() => new(
        Summary: "Summary of changes.",
        RiskScore: 70,
        ModelProvider: "OpenAI",
        ModelVersion: "gpt-4o-mini",
        TokensUsed: 123,
        Findings: [new FindingDraft(Severity.Major, FindingCategory.Bug, "a.cs", 1, "title", "desc", null)],
        Prompts: [new PromptTrace("analyze", "gpt-4o-mini", "review this")],
        RawResponse: """{"summary":"Summary of changes.","findings":[]}""");

    [Fact]
    public async Task Successful_pipeline_completes_assessment_and_awaits_human_review()
    {
        var ctx = new Harness().Seed();

        await ctx.Handler(new StubReviewPipeline(Assessment())).HandleAsync(ctx.Review.Id);

        Assert.Equal(ReviewStatus.AwaitingHumanReview, ctx.Review.Status);
        Assert.Single(ctx.Review.Findings);
        Assert.Equal("Summary of changes.", ctx.Review.Summary);
        Assert.Equal("raw-ref-1", ctx.Review.RawResultRef);
    }

    [Fact]
    public async Task Pipeline_failure_marks_review_failed()
    {
        var ctx = new Harness().Seed();

        await ctx.Handler(new ThrowingReviewPipeline()).HandleAsync(ctx.Review.Id);

        Assert.Equal(ReviewStatus.Failed, ctx.Review.Status);
    }

    [Fact]
    public async Task Non_pending_review_is_skipped()
    {
        var ctx = new Harness().Seed();
        ctx.Review.StartProcessing(); // already Processing

        await ctx.Handler(new StubReviewPipeline(Assessment())).HandleAsync(ctx.Review.Id);

        Assert.Equal(ReviewStatus.Processing, ctx.Review.Status); // unchanged
    }
}
