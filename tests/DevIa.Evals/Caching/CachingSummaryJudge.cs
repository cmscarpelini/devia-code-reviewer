using DevIa.Evals.Runner;

namespace DevIa.Evals.Caching;

/// <summary>
/// Caches judge scores per (prompt version, model, summary, diff), so re-runs don't re-spend the
/// judge call either. Shares the prompt version with the pipeline cache.
/// </summary>
public sealed class CachingSummaryJudge(
    ISummaryJudge inner, JsonFileResultCache cache, string model, string promptVersion) : ISummaryJudge
{
    public Task<double?> ScoreAsync(string diff, string summary, CancellationToken cancellationToken = default)
    {
        var key = $"judge|{promptVersion}|{model}|{summary}|{diff}";
        return cache.GetOrAddAsync(key, () => inner.ScoreAsync(diff, summary, cancellationToken), cancellationToken);
    }
}
