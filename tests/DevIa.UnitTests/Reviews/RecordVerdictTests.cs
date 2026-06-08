using DevIa.Application.Abstractions.Persistence;
using DevIa.Application.Reviews;
using DevIa.Domain.Audit;
using DevIa.Domain.Common;
using DevIa.Domain.Enums;
using DevIa.Domain.Identity;
using DevIa.Domain.PullRequests;
using DevIa.Domain.Reviews;
using DevIa.UnitTests.PullRequests; // reuse the repository fakes
using Microsoft.Extensions.Logging.Abstractions;

namespace DevIa.UnitTests.Reviews;

public class RecordVerdictTests
{
    private sealed class FakeAuditLogRepository : IAuditLogRepository
    {
        public readonly List<AuditLog> Items = [];
        public void Add(AuditLog auditLog) => Items.Add(auditLog);
    }

    private sealed class FakeVerdictNotifier : IVerdictNotifier
    {
        public readonly List<VerdictNotification> Published = [];
        public Task PublishAsync(VerdictNotification notification, CancellationToken ct = default)
        {
            Published.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class Harness
    {
        public readonly FakeReviewRepository Reviews = new();
        public readonly FakePullRequestRepository Prs = new();
        public readonly FakeCodeRepositoryRepository Repos = new();
        public readonly FakeAuditLogRepository Audits = new();
        public readonly FakeVerdictNotifier Notifier = new();
        public readonly FakeUnitOfWork Uow = new();
        public Review Review = default!;

        public Harness SeedAwaiting(IEnumerable<Finding>? findings = null)
        {
            var repository = new CodeRepository(Guid.NewGuid(), 200, "acme/app", "main");
            var pullRequest = new PullRequest(repository.Id, 7, Guid.NewGuid(), "title", "main", "https://x/pull/7", "open");
            Review = new Review(pullRequest.Id, "sha1");
            Review.StartProcessing();
            Review.CompleteAssessment("summary", 10, "OpenAI", "gpt-4o-mini", 10, "raw-ref", findings ?? []);

            Repos.Add(repository);
            Prs.Add(pullRequest);
            Reviews.Add(Review);
            return this;
        }

        public RecordVerdict Handler() => new(
            Reviews, Prs, Repos, Audits, Notifier, Uow, NullLogger<RecordVerdict>.Instance);
    }

    [Fact]
    public async Task Approve_records_verdict_writes_audit_and_notifies_github()
    {
        var ctx = new Harness().SeedAwaiting();
        var reviewer = Guid.NewGuid();

        var result = await ctx.Handler().HandleAsync(ctx.Review.Id, reviewer, VerdictDecision.Approved, null);

        Assert.Equal(RecordVerdictStatus.Recorded, result.Status);
        Assert.Equal(ReviewStatus.Approved, ctx.Review.Status);
        Assert.NotNull(ctx.Review.Verdict);
        Assert.Equal(reviewer, ctx.Review.Verdict!.ReviewerUserId);
        Assert.Single(ctx.Audits.Items);
        Assert.Single(ctx.Notifier.Published);
    }

    [Fact]
    public async Task Reject_with_justification_sets_rejected()
    {
        var ctx = new Harness().SeedAwaiting();

        var result = await ctx.Handler().HandleAsync(ctx.Review.Id, Guid.NewGuid(), VerdictDecision.Rejected, "Missing tests.");

        Assert.Equal(RecordVerdictStatus.Recorded, result.Status);
        Assert.Equal(ReviewStatus.Rejected, ctx.Review.Status);
        Assert.Equal("Missing tests.", ctx.Review.Verdict!.Justification);
    }

    [Fact]
    public async Task Reject_without_justification_throws_and_records_nothing()
    {
        var ctx = new Harness().SeedAwaiting();

        await Assert.ThrowsAsync<DomainException>(
            () => ctx.Handler().HandleAsync(ctx.Review.Id, Guid.NewGuid(), VerdictDecision.Rejected, null));

        Assert.Equal(ReviewStatus.AwaitingHumanReview, ctx.Review.Status);
        Assert.Empty(ctx.Audits.Items);
        Assert.Empty(ctx.Notifier.Published);
    }

    [Fact]
    public async Task Reject_without_justification_but_with_ai_consent_uses_ai_analysis_as_justification()
    {
        var finding = new Finding(
            Severity.Major, FindingCategory.Security, "src/auth.cs", 42,
            "Hardcoded secret", "A credential is committed in source.", "Move it to a secret store.");
        var ctx = new Harness().SeedAwaiting([finding]);

        var result = await ctx.Handler().HandleAsync(
            ctx.Review.Id, Guid.NewGuid(), VerdictDecision.Rejected, null,
            useAiAnalysisWhenNoJustification: true);

        Assert.Equal(RecordVerdictStatus.Recorded, result.Status);
        Assert.Equal(ReviewStatus.Rejected, ctx.Review.Status);

        var justification = ctx.Review.Verdict!.Justification;
        Assert.NotNull(justification);
        Assert.Contains("summary", justification);          // the AI summary
        Assert.Contains("Hardcoded secret", justification);  // the finding title
        Assert.Contains("src/auth.cs:42", justification);    // the finding location

        // The same AI-built justification is what gets reflected on GitHub.
        Assert.Single(ctx.Notifier.Published);
        Assert.Equal(justification, ctx.Notifier.Published[0].Justification);
    }

    [Fact]
    public async Task Reject_without_justification_and_without_consent_still_throws()
    {
        var ctx = new Harness().SeedAwaiting([
            new Finding(Severity.Major, FindingCategory.Bug, "f.cs", 1, "t", "d")]);

        await Assert.ThrowsAsync<DomainException>(
            () => ctx.Handler().HandleAsync(
                ctx.Review.Id, Guid.NewGuid(), VerdictDecision.Rejected, null,
                useAiAnalysisWhenNoJustification: false));

        Assert.Equal(ReviewStatus.AwaitingHumanReview, ctx.Review.Status);
        Assert.Empty(ctx.Notifier.Published);
    }

    [Fact]
    public async Task Missing_review_returns_not_found()
    {
        var ctx = new Harness(); // not seeded

        var result = await ctx.Handler().HandleAsync(Guid.NewGuid(), Guid.NewGuid(), VerdictDecision.Approved, null);

        Assert.Equal(RecordVerdictStatus.ReviewNotFound, result.Status);
    }

    [Fact]
    public async Task Second_verdict_throws()
    {
        var ctx = new Harness().SeedAwaiting();
        await ctx.Handler().HandleAsync(ctx.Review.Id, Guid.NewGuid(), VerdictDecision.Approved, null);

        await Assert.ThrowsAsync<DomainException>(
            () => ctx.Handler().HandleAsync(ctx.Review.Id, Guid.NewGuid(), VerdictDecision.Approved, null));
    }
}
