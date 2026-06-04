using DevIa.Evals.Scoring;

namespace DevIa.Evals.Runner;

/// <summary>
/// A single finding the pipeline reported, tagged with its adjudication so the report is
/// diagnosable: <c>Outcome</c> is "TP" (matched a defect) or "FP" (spurious). For a TP,
/// <c>ExpectedSeverity</c> is the ground-truth severity to compare against <c>Severity</c>.
/// </summary>
public sealed record ReportedFinding(
    string File,
    int? Line,
    string Severity,
    string Category,
    string Title,
    string Outcome,
    string? ExpectedSeverity);

/// <summary>Per-case outcome for the report (counts, the judged summary, and the findings).</summary>
public sealed record CaseReport(
    string CaseId,
    bool IsClean,
    int ReportedFindings,
    int TruePositives,
    int FalsePositives,
    int FalseNegatives,
    double? JudgeScore,
    string Summary,
    IReadOnlyList<ReportedFinding> Findings);

/// <summary>
/// A full eval run: the aggregate metrics, the optional average LLM-as-judge score, and the
/// per-case breakdown. Serialized to <c>reports/</c> for trend tracking.
/// </summary>
public sealed record EvalReport(
    DateTimeOffset RunAt,
    string Provider,
    string Model,
    EvalMetrics Metrics,
    double? AverageJudgeScore,
    IReadOnlyList<CaseReport> Cases);
