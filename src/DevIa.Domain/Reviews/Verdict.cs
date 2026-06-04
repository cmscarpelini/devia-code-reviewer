using DevIa.Domain.Common;
using DevIa.Domain.Enums;

namespace DevIa.Domain.Reviews;

/// <summary>
/// The human decision on a review. Immutable once created and only produced by the
/// <see cref="Review"/> aggregate root (constructor is <c>internal</c>).
/// </summary>
public sealed class Verdict : Entity
{
    public Guid ReviewId { get; private set; }
    public Guid ReviewerUserId { get; private set; }
    public VerdictDecision Decision { get; private set; }
    public string? Justification { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private Verdict() { } // EF

    internal Verdict(Guid reviewId, Guid reviewerUserId, VerdictDecision decision, string? justification)
    {
        Id = Guid.NewGuid();
        ReviewId = reviewId;
        ReviewerUserId = reviewerUserId;
        Decision = decision;
        Justification = justification;
        CreatedAt = DateTimeOffset.UtcNow;
    }
}
