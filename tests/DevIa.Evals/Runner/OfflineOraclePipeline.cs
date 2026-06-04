using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using DevIa.Evals.Dataset;

namespace DevIa.Evals.Runner;

/// <summary>
/// A deterministic, no-LLM pipeline used by <c>--offline</c> smoke runs: it returns each case's
/// ground-truth defects as findings (keyed by diff). This exercises the full runner/matcher/metrics/
/// report path without network or cost, and serves as a sanity check that a perfect reviewer scores 1.0.
/// </summary>
public sealed class OfflineOraclePipeline : IReviewPipeline
{
    private readonly Dictionary<string, ReviewAssessment> _byDiff;

    public OfflineOraclePipeline(IReadOnlyList<EvalCase> cases)
    {
        _byDiff = cases.ToDictionary(c => c.Diff, ToAssessment);
    }

    public Task<ReviewAssessment> RunAsync(ReviewPipelineInput input, CancellationToken cancellationToken = default)
        => Task.FromResult(_byDiff.TryGetValue(input.Diff, out var assessment)
            ? assessment
            : Empty());

    private static ReviewAssessment ToAssessment(EvalCase evalCase)
    {
        var findings = evalCase.Expected.Defects
            .Select(d => new FindingDraft(
                ParseSeverity(d.Severity), ParseCategory(d.Category),
                d.File, d.Line, d.Description ?? "defect", d.Description ?? "", null))
            .ToList();

        return new ReviewAssessment(
            Summary: $"Offline oracle for {evalCase.Id}.",
            RiskScore: null, ModelProvider: "Offline", ModelVersion: "oracle", TokensUsed: 0,
            Findings: findings, Prompts: [], RawResponse: "");
    }

    private static ReviewAssessment Empty() => new(
        "No findings.", 0, "Offline", "oracle", 0, [], [], "");

    private static Severity ParseSeverity(string value) =>
        Enum.TryParse<Severity>(value, ignoreCase: true, out var v) ? v : Severity.Minor;

    private static FindingCategory ParseCategory(string value) =>
        Enum.TryParse<FindingCategory>(value, ignoreCase: true, out var v) ? v : FindingCategory.Bug;
}
