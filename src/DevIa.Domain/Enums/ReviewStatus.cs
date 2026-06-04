namespace DevIa.Domain.Enums;

/// <summary>Lifecycle state of a <c>Review</c> (see SPEC-0001 state machine).</summary>
public enum ReviewStatus
{
    Pending,
    Processing,
    AwaitingHumanReview,
    Approved,
    Rejected,
    Failed
}
