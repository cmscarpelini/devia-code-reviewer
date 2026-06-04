using DevIa.Evals.Scoring;

namespace DevIa.Evals.Runner;

/// <summary>Threshold gates for the eval regression suite (ADR-0005). Tune over time.</summary>
public sealed record EvalGateOptions(double MinRecall, double MaxFalsePositiveRate);

/// <summary>The result of checking metrics against the gates.</summary>
public sealed record GateResult(bool Passed, IReadOnlyList<string> Failures);

public static class EvalGates
{
    public static GateResult Evaluate(EvalMetrics metrics, EvalGateOptions options)
        => Evaluate(metrics.Recall, metrics.FalsePositiveRate, options);

    /// <summary>Checks recall + FP rate against the gates. Multi-run callers pass the means.</summary>
    public static GateResult Evaluate(double recall, double falsePositiveRate, EvalGateOptions options)
    {
        var failures = new List<string>();

        if (recall < options.MinRecall)
            failures.Add($"Recall {recall:P1} < minimum {options.MinRecall:P1}.");

        if (falsePositiveRate > options.MaxFalsePositiveRate)
            failures.Add($"False-positive rate {falsePositiveRate:0.00}/clean-PR > max {options.MaxFalsePositiveRate:0.00}.");

        return new GateResult(failures.Count == 0, failures);
    }
}
