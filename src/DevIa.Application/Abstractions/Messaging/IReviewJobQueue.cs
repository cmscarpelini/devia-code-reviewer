namespace DevIa.Application.Abstractions.Messaging;

/// <summary>Message enqueued for the Worker to process a review (SPEC-0001).</summary>
public sealed record ReviewQueuedMessage(Guid ReviewId, Guid PullRequestId, string HeadSha);

/// <summary>
/// Port for publishing review jobs. The concrete adapter is chosen by configuration
/// (a logging stub in B1; the RabbitMQ publisher arrives in B2).
/// </summary>
public interface IReviewJobQueue
{
    Task EnqueueAsync(ReviewQueuedMessage message, CancellationToken cancellationToken = default);
}
