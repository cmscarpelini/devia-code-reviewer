using DevIa.Evals.Scoring;

namespace DevIa.Evals.Runner;

/// <summary>
/// How stable one case was across the runs: how often its defects were fully caught
/// (<see cref="FullDetectionRate"/>) and, for clean cases, how often it stayed false-positive-free
/// (<see cref="CleanRate"/>). Surfaces flaky cases the single-run report hides.
/// </summary>
public sealed record CaseStability(
    string CaseId,
    bool IsClean,
    int Runs,
    double FullDetectionRate,
    double CleanRate,
    double AvgTruePositives,
    double AvgFalsePositives,
    double AvgFalseNegatives);

/// <summary>
/// Aggregates N <see cref="EvalReport"/>s into mean ± spread per metric, plus per-case stability.
/// The right lens for a non-deterministic pipeline: trends, not a single run.
/// </summary>
public sealed record AggregateReport(
    DateTimeOffset RunAt,
    string Provider,
    string Model,
    int Runs,
    MetricSummary Recall,
    MetricSummary Precision,
    MetricSummary F1,
    MetricSummary FalsePositiveRate,
    MetricSummary SeverityAccuracy,
    MetricSummary CategoryAccuracy,
    MetricSummary? Judge,
    IReadOnlyList<CaseStability> Cases)
{
    public static AggregateReport From(IReadOnlyList<EvalReport> runs)
    {
        if (runs.Count == 0)
            throw new ArgumentException("At least one run is required.", nameof(runs));

        var first = runs[0];

        var judgeValues = runs.Where(r => r.AverageJudgeScore is not null)
            .Select(r => r.AverageJudgeScore!.Value).ToList();

        var cases = runs
            .SelectMany(r => r.Cases)
            .GroupBy(c => c.CaseId)
            .OrderBy(g => g.Key)
            .Select(g => StabilityOf(g.Key, g.ToList()))
            .ToList();

        return new AggregateReport(
            DateTimeOffset.UtcNow, first.Provider, first.Model, runs.Count,
            MetricSummary.From(runs.Select(r => r.Metrics.Recall).ToList()),
            MetricSummary.From(runs.Select(r => r.Metrics.Precision).ToList()),
            MetricSummary.From(runs.Select(r => r.Metrics.F1).ToList()),
            MetricSummary.From(runs.Select(r => r.Metrics.FalsePositiveRate).ToList()),
            MetricSummary.From(runs.Select(r => r.Metrics.SeverityAccuracy).ToList()),
            MetricSummary.From(runs.Select(r => r.Metrics.CategoryAccuracy).ToList()),
            judgeValues.Count == 0 ? null : MetricSummary.From(judgeValues),
            cases);
    }

    private static CaseStability StabilityOf(string caseId, IReadOnlyList<CaseReport> perRun)
    {
        var n = perRun.Count;
        return new CaseStability(
            caseId,
            perRun[0].IsClean,
            n,
            FullDetectionRate: perRun.Count(c => c.FalseNegatives == 0) / (double)n,
            CleanRate: perRun.Count(c => c.FalsePositives == 0) / (double)n,
            AvgTruePositives: perRun.Average(c => c.TruePositives),
            AvgFalsePositives: perRun.Average(c => c.FalsePositives),
            AvgFalseNegatives: perRun.Average(c => c.FalseNegatives));
    }
}
