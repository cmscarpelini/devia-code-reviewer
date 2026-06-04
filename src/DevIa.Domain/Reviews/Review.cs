using DevIa.Domain.Common;
using DevIa.Domain.Enums;

namespace DevIa.Domain.Reviews;

/// <summary>
/// Aggregate root for a review of a PR version. Owns its <see cref="Finding"/> collection
/// and its single <see cref="Verdict"/>, and enforces the lifecycle (SPEC-0001) and the
/// verdict rules (SPEC-0003).
/// </summary>
public sealed class Review : Entity
{
    public Guid PullRequestId { get; private set; }
    public string HeadSha { get; private set; } = null!;
    public ReviewStatus Status { get; private set; }
    public string? Summary { get; private set; }
    public int? RiskScore { get; private set; }
    public string? ModelProvider { get; private set; }
    public string? ModelVersion { get; private set; }
    public int? TokensUsed { get; private set; }
    public string? RawResultRef { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }

    private readonly List<Finding> _findings = [];
    public IReadOnlyCollection<Finding> Findings => _findings.AsReadOnly();

    public Verdict? Verdict { get; private set; }

    private Review() { } // EF

    public Review(Guid pullRequestId, string headSha)
    {
        if (pullRequestId == default) throw new DomainException("PullRequestId is required.");
        if (string.IsNullOrWhiteSpace(headSha)) throw new DomainException("HeadSha is required.");

        Id = Guid.NewGuid();
        PullRequestId = pullRequestId;
        HeadSha = headSha;
        Status = ReviewStatus.Pending;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Pending → Processing (the worker picked up the job).</summary>
    public void StartProcessing()
    {
        if (Status != ReviewStatus.Pending)
            throw new DomainException($"Cannot start processing from status {Status}.");
        Status = ReviewStatus.Processing;
    }

    /// <summary>Processing → AwaitingHumanReview (the assessment was generated).</summary>
    public void CompleteAssessment(
        string summary,
        int? riskScore,
        string modelProvider,
        string modelVersion,
        int tokensUsed,
        string rawResultRef,
        IEnumerable<Finding> findings)
    {
        if (Status != ReviewStatus.Processing)
            throw new DomainException($"Cannot complete the assessment from status {Status}.");
        if (string.IsNullOrWhiteSpace(summary)) throw new DomainException("Summary is required.");
        if (string.IsNullOrWhiteSpace(rawResultRef)) throw new DomainException("RawResultRef is required.");

        Summary = summary;
        RiskScore = riskScore;
        ModelProvider = modelProvider;
        ModelVersion = modelVersion;
        TokensUsed = tokensUsed;
        RawResultRef = rawResultRef;

        _findings.Clear();
        _findings.AddRange(findings);

        Status = ReviewStatus.AwaitingHumanReview;
        CompletedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Any non-decided state → Failed (the pipeline errored); reprocessable.</summary>
    public void Fail()
    {
        if (Status is ReviewStatus.Approved or ReviewStatus.Rejected)
            throw new DomainException($"Cannot fail a decided review (status {Status}).");
        Status = ReviewStatus.Failed;
    }

    /// <summary>Failed → Pending (retry).</summary>
    public void Reprocess()
    {
        if (Status != ReviewStatus.Failed)
            throw new DomainException($"Cannot reprocess from status {Status}.");
        Status = ReviewStatus.Pending;
    }

    /// <summary>
    /// Records the human verdict (SPEC-0003): only from AwaitingHumanReview, only once,
    /// and a justification is required to reject.
    /// </summary>
    public Verdict RecordVerdict(Guid reviewerUserId, VerdictDecision decision, string? justification)
    {
        if (reviewerUserId == default) throw new DomainException("ReviewerUserId is required.");
        if (Status != ReviewStatus.AwaitingHumanReview)
            throw new DomainException($"Cannot record a verdict from status {Status}.");
        if (Verdict is not null)
            throw new DomainException("This review already has a verdict.");
        if (decision == VerdictDecision.Rejected && string.IsNullOrWhiteSpace(justification))
            throw new DomainException("A justification is required to reject a review.");

        Verdict = new Verdict(Id, reviewerUserId, decision, justification);
        Status = decision == VerdictDecision.Approved ? ReviewStatus.Approved : ReviewStatus.Rejected;
        return Verdict;
    }
}
