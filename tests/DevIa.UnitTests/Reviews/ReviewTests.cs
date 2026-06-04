using DevIa.Domain.Common;
using DevIa.Domain.Enums;
using DevIa.Domain.Reviews;

namespace DevIa.UnitTests.Reviews;

public class ReviewTests
{
    private static Review NewPending() => new(Guid.NewGuid(), "abc123sha");

    private static Review NewProcessing()
    {
        var review = NewPending();
        review.StartProcessing();
        return review;
    }

    private static Review NewAwaiting()
    {
        var review = NewProcessing();
        review.CompleteAssessment(
            summary: "Changed the user service.",
            riskScore: 12,
            modelProvider: "AzureOpenAI",
            modelVersion: "gpt-4o-mini",
            tokensUsed: 512,
            rawResultRef: "mongo-object-id",
            findings: [new Finding(Severity.Major, FindingCategory.Bug, "src/UserService.cs", 42, "Null deref", "Possible null dereference.")]);
        return review;
    }

    // --- Construction ---

    [Fact]
    public void New_review_starts_pending()
    {
        var review = NewPending();

        Assert.Equal(ReviewStatus.Pending, review.Status);
        Assert.NotEqual(Guid.Empty, review.Id);
        Assert.Empty(review.Findings);
        Assert.Null(review.Verdict);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void New_review_requires_head_sha(string headSha)
        => Assert.Throws<DomainException>(() => new Review(Guid.NewGuid(), headSha));

    [Fact]
    public void New_review_requires_pull_request_id()
        => Assert.Throws<DomainException>(() => new Review(Guid.Empty, "sha"));

    // --- StartProcessing ---

    [Fact]
    public void StartProcessing_moves_pending_to_processing()
    {
        var review = NewPending();

        review.StartProcessing();

        Assert.Equal(ReviewStatus.Processing, review.Status);
    }

    [Fact]
    public void StartProcessing_is_invalid_when_not_pending()
    {
        var review = NewProcessing();

        Assert.Throws<DomainException>(review.StartProcessing);
    }

    // --- CompleteAssessment ---

    [Fact]
    public void CompleteAssessment_moves_processing_to_awaiting_and_sets_fields()
    {
        var review = NewAwaiting();

        Assert.Equal(ReviewStatus.AwaitingHumanReview, review.Status);
        Assert.Equal("Changed the user service.", review.Summary);
        Assert.Equal("gpt-4o-mini", review.ModelVersion);
        Assert.Equal("mongo-object-id", review.RawResultRef);
        Assert.Single(review.Findings);
        Assert.NotNull(review.CompletedAt);
    }

    [Fact]
    public void CompleteAssessment_is_invalid_when_not_processing()
    {
        var review = NewPending();

        Assert.Throws<DomainException>(() => review.CompleteAssessment(
            "s", null, "p", "m", 1, "ref", []));
    }

    [Fact]
    public void CompleteAssessment_requires_raw_result_ref()
    {
        var review = NewProcessing();

        Assert.Throws<DomainException>(() => review.CompleteAssessment(
            "summary", null, "p", "m", 1, rawResultRef: " ", findings: []));
    }

    // --- Fail / Reprocess ---

    [Fact]
    public void Fail_marks_review_failed()
    {
        var review = NewProcessing();

        review.Fail();

        Assert.Equal(ReviewStatus.Failed, review.Status);
    }

    [Fact]
    public void Failed_review_can_be_reprocessed_back_to_pending()
    {
        var review = NewProcessing();
        review.Fail();

        review.Reprocess();

        Assert.Equal(ReviewStatus.Pending, review.Status);
    }

    [Fact]
    public void Decided_review_cannot_fail()
    {
        var review = NewAwaiting();
        review.RecordVerdict(Guid.NewGuid(), VerdictDecision.Approved, null);

        Assert.Throws<DomainException>(review.Fail);
    }

    // --- RecordVerdict (SPEC-0003) ---

    [Fact]
    public void Approve_records_verdict_and_sets_status_approved()
    {
        var review = NewAwaiting();
        var reviewer = Guid.NewGuid();

        var verdict = review.RecordVerdict(reviewer, VerdictDecision.Approved, null);

        Assert.Equal(ReviewStatus.Approved, review.Status);
        Assert.Same(verdict, review.Verdict);
        Assert.Equal(reviewer, verdict.ReviewerUserId);
        Assert.Equal(review.Id, verdict.ReviewId);
    }

    [Fact]
    public void Reject_requires_a_justification()
    {
        var review = NewAwaiting();

        Assert.Throws<DomainException>(() =>
            review.RecordVerdict(Guid.NewGuid(), VerdictDecision.Rejected, justification: null));
        Assert.Equal(ReviewStatus.AwaitingHumanReview, review.Status);
        Assert.Null(review.Verdict);
    }

    [Fact]
    public void Reject_with_justification_sets_status_rejected()
    {
        var review = NewAwaiting();

        review.RecordVerdict(Guid.NewGuid(), VerdictDecision.Rejected, "Missing tests.");

        Assert.Equal(ReviewStatus.Rejected, review.Status);
        Assert.Equal("Missing tests.", review.Verdict!.Justification);
    }

    [Fact]
    public void Verdict_can_only_be_recorded_once()
    {
        var review = NewAwaiting();
        review.RecordVerdict(Guid.NewGuid(), VerdictDecision.Approved, null);

        Assert.Throws<DomainException>(() =>
            review.RecordVerdict(Guid.NewGuid(), VerdictDecision.Approved, null));
    }

    [Fact]
    public void Verdict_is_invalid_when_not_awaiting_human_review()
    {
        var review = NewProcessing();

        Assert.Throws<DomainException>(() =>
            review.RecordVerdict(Guid.NewGuid(), VerdictDecision.Approved, null));
    }
}
