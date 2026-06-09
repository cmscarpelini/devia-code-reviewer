using DevIa.Evals.Runner;
using DevIa.Evals.Scoring;

namespace DevIa.UnitTests.Evals;

/// <summary>
/// Deterministic tests for multi-run aggregation (mean/spread + per-case stability). The LLM is
/// non-deterministic, so the aggregation math that summarizes N runs must itself be exact.
/// </summary>
public class EvalAggregateTests
{
    [Fact]
    public void MetricSummary_computes_mean_min_max_and_population_stddev()
    {
        var summary = MetricSummary.From([0.8, 0.6, 1.0]);

        Assert.Equal(0.8, summary.Mean, 6);
        Assert.Equal(0.6, summary.Min, 6);
        Assert.Equal(1.0, summary.Max, 6);
        // population stddev = sqrt(((0)^2 + (0.2)^2 + (0.2)^2) / 3)
        Assert.Equal(Math.Sqrt(0.08 / 3), summary.StdDev, 6);
    }

    [Fact]
    public void MetricSummary_of_single_value_has_zero_spread()
    {
        var summary = MetricSummary.From([0.5]);

        Assert.Equal(0.5, summary.Mean, 6);
        Assert.Equal(0, summary.StdDev, 6);
    }

    [Fact]
    public void MetricSummary_of_empty_is_all_zero()
    {
        var summary = MetricSummary.From([]);

        Assert.Equal(new MetricSummary(0, 0, 0, 0), summary);
    }

    private static EvalReport Run(double recall, CaseReport[] cases, double? judge) =>
        new(DateTimeOffset.UtcNow, "GitHubModels", "m",
            new EvalMetrics(0, 0, 0, recall, 1, 1, 0, 1, 1, 1, cases.Length),
            judge, cases);

    private static CaseReport Case(string id, bool clean, int tp, int fp, int fn) =>
        new(id, clean, tp + fp, tp, fp, fn, null, "", []);

    [Fact]
    public void Aggregate_summarizes_metrics_and_per_case_stability_across_runs()
    {
        // Run 1: defect detected, clean stayed clean. Run 2: defect missed, clean had an FP.
        var run1 = Run(1.0, [Case("d1", clean: false, tp: 1, fp: 0, fn: 0), Case("c1", clean: true, 0, 0, 0)], judge: 4.0);
        var run2 = Run(0.5, [Case("d1", clean: false, tp: 0, fp: 0, fn: 1), Case("c1", clean: true, 0, 1, 0)], judge: 5.0);

        var aggregate = AggregateReport.From([run1, run2]);

        Assert.Equal(2, aggregate.Runs);
        Assert.Equal(0.75, aggregate.Recall.Mean, 6);
        Assert.Equal(4.5, aggregate.Judge!.Mean, 6);

        var defect = aggregate.Cases.Single(c => c.CaseId == "d1");
        Assert.Equal(0.5, defect.FullDetectionRate, 6);   // caught in 1 of 2 runs
        Assert.Equal(0.5, defect.AvgTruePositives, 6);

        var clean = aggregate.Cases.Single(c => c.CaseId == "c1");
        Assert.Equal(0.5, clean.CleanRate, 6);            // FP-free in 1 of 2 runs
        Assert.Equal(0.5, clean.AvgFalsePositives, 6);
    }

    [Fact]
    public void Aggregate_judge_is_null_when_no_run_scored_a_summary()
    {
        var run = Run(1.0, [Case("d1", false, 1, 0, 0)], judge: null);

        Assert.Null(AggregateReport.From([run]).Judge);
    }
}
