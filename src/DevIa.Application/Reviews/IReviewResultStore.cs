namespace DevIa.Application.Reviews;

/// <summary>
/// Persists the raw review result (diff, prompts, full findings) and returns a reference id
/// stored on the review (<c>RawResultRef</c>). The MongoDB adapter arrives in a later slice;
/// for now a no-op store returns a generated id.
/// </summary>
public interface IReviewResultStore
{
    Task<string> SaveAsync(Guid reviewId, ReviewPipelineInput input, ReviewAssessment assessment, CancellationToken cancellationToken = default);
}
