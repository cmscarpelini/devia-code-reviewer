namespace DevIa.Evals.Scoring;

/// <summary>
/// Summary statistics for a metric measured across N eval runs. <see cref="StdDev"/> is the
/// population standard deviation (divides by N), so a single run yields 0 rather than NaN —
/// the LLM is non-deterministic even at temperature 0, so multiple runs reveal the spread.
/// </summary>
public sealed record MetricSummary(double Mean, double StdDev, double Min, double Max)
{
    public static MetricSummary From(IReadOnlyList<double> values)
    {
        if (values.Count == 0)
            return new MetricSummary(0, 0, 0, 0);

        var mean = values.Average();
        var variance = values.Sum(v => (v - mean) * (v - mean)) / values.Count;
        return new MetricSummary(mean, Math.Sqrt(variance), values.Min(), values.Max());
    }
}
