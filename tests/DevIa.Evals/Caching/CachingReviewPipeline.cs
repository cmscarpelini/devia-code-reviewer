using DevIa.Application.Reviews;

namespace DevIa.Evals.Caching;

/// <summary>
/// Caches the inner pipeline's assessment per (prompt version, model, diff), so re-running the
/// harness without changing the prompt/model costs nothing. Bump the prompt version (config
/// <c>Llm:PromptVersion</c>) whenever the prompt template changes, to invalidate stale entries.
/// </summary>
public sealed class CachingReviewPipeline(
    IReviewPipeline inner, JsonFileResultCache cache, string model, string promptVersion) : IReviewPipeline
{
    public Task<ReviewAssessment> RunAsync(ReviewPipelineInput input, CancellationToken cancellationToken = default)
    {
        var key = $"pipeline|{promptVersion}|{model}|{input.Diff}";
        return cache.GetOrAddAsync(key, () => inner.RunAsync(input, cancellationToken), cancellationToken);
    }
}
