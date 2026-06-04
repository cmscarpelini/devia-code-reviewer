namespace DevIa.Application.PullRequests;

public enum WebhookProcessingStatus
{
    /// <summary>A new review was created and enqueued.</summary>
    Accepted,

    /// <summary>A review for this (PR, head SHA) already existed — no-op (idempotency).</summary>
    Duplicate,

    /// <summary>The action is not one we handle (e.g., closed).</summary>
    Ignored
}

public sealed record WebhookProcessingResult(WebhookProcessingStatus Status, Guid? ReviewId)
{
    public static WebhookProcessingResult Accepted(Guid reviewId) => new(WebhookProcessingStatus.Accepted, reviewId);
    public static WebhookProcessingResult Duplicate(Guid reviewId) => new(WebhookProcessingStatus.Duplicate, reviewId);
    public static WebhookProcessingResult Ignored() => new(WebhookProcessingStatus.Ignored, null);
}
