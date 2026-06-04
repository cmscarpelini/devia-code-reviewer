namespace DevIa.Evals.Scoring;

/// <summary>The match outcome for one case, tagged with whether the case is a clean PR.</summary>
public sealed record CaseOutcome(string CaseId, bool IsClean, MatchResult Match);

/// <summary>
/// Aggregate AI-quality metrics over a dataset run (ADR-0005). Rates are in [0,1]; the
/// false-positive rate is findings-per-clean-PR (not a ratio).
/// </summary>
public sealed record EvalMetrics(
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    double Recall,
    double Precision,
    double F1,
    double FalsePositiveRate,
    double SeverityAccuracy,
    int CleanCases,
    int TotalCases)
{
    /// <summary>Computes the aggregate metrics from per-case match outcomes.</summary>
    public static EvalMetrics From(IReadOnlyList<CaseOutcome> outcomes)
    {
        var tp = outcomes.Sum(o => o.Match.Matched.Count);
        var fp = outcomes.Sum(o => o.Match.FalsePositives.Count);
        var fn = outcomes.Sum(o => o.Match.FalseNegatives.Count);

        var recall = Ratio(tp, tp + fn);
        var precision = Ratio(tp, tp + fp);
        var f1 = (precision + recall) == 0 ? 0 : 2 * precision * recall / (precision + recall);

        var cleanCases = outcomes.Count(o => o.IsClean);
        var falsePositivesOnClean = outcomes.Where(o => o.IsClean).Sum(o => o.Match.FalsePositives.Count);
        var fpRate = cleanCases == 0 ? 0 : (double)falsePositivesOnClean / cleanCases;

        var allMatched = outcomes.SelectMany(o => o.Match.Matched).ToList();
        var correctSeverity = allMatched.Count(p =>
            string.Equals(p.Reported.Severity.ToString(), p.Expected.Severity, StringComparison.OrdinalIgnoreCase));
        var severityAccuracy = Ratio(correctSeverity, allMatched.Count);

        return new EvalMetrics(
            tp, fp, fn, recall, precision, f1, fpRate, severityAccuracy, cleanCases, outcomes.Count);
    }

    private static double Ratio(int numerator, int denominator) =>
        denominator == 0 ? 0 : (double)numerator / denominator;
}
