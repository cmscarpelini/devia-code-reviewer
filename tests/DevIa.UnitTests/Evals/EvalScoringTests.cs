using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using DevIa.Evals.Dataset;
using DevIa.Evals.Scoring;

namespace DevIa.UnitTests.Evals;

/// <summary>
/// Deterministic tests for the eval scoring core (ADR-0005, Layer 1). The matcher + metrics math
/// must be correct — they are "our code", verifiable without any LLM.
/// </summary>
public class EvalScoringTests
{
    private static FindingDraft Finding(
        string file, int? line, FindingCategory category = FindingCategory.Bug, Severity severity = Severity.Major)
        => new(severity, category, file, line, "title", "desc", null);

    private static ExpectedDefect Defect(
        string file, int? line, string category = "Bug", string severity = "Major")
        => new(file, line, category, severity, "desc");

    // ---- Matcher ----

    [Fact]
    public void Matches_on_same_file_category_and_nearby_line()
    {
        var result = FindingMatcher.Match(
            [Finding("src/A.cs", 42)],
            [Defect("src/A.cs", 44)]); // within default tolerance of 3

        var pair = Assert.Single(result.Matched);
        Assert.Equal(42, pair.Reported.Line);
        Assert.Empty(result.FalsePositives);
        Assert.Empty(result.FalseNegatives);
    }

    [Fact]
    public void Line_outside_tolerance_is_not_a_match()
    {
        var result = FindingMatcher.Match([Finding("src/A.cs", 10)], [Defect("src/A.cs", 50)]);

        Assert.Empty(result.Matched);
        Assert.Single(result.FalsePositives);
        Assert.Single(result.FalseNegatives);
    }

    [Fact]
    public void Category_mismatch_is_not_a_match()
    {
        var result = FindingMatcher.Match(
            [Finding("src/A.cs", 42, FindingCategory.Style)],
            [Defect("src/A.cs", 42, category: "Bug")]);

        Assert.Empty(result.Matched);
    }

    [Fact]
    public void Different_file_is_not_a_match()
    {
        var result = FindingMatcher.Match([Finding("src/A.cs", 42)], [Defect("src/B.cs", 42)]);

        Assert.Empty(result.Matched);
    }

    [Fact]
    public void File_level_defect_with_null_line_matches_any_line()
    {
        var result = FindingMatcher.Match([Finding("src/A.cs", 999)], [Defect("src/A.cs", null)]);

        Assert.Single(result.Matched);
    }

    [Fact]
    public void File_paths_are_compared_normalized_across_separators()
    {
        var result = FindingMatcher.Match([Finding("src\\A.cs", 1)], [Defect("src/A.cs", 1)]);

        Assert.Single(result.Matched);
    }

    [Fact]
    public void Greedy_match_assigns_the_closest_finding_to_each_defect()
    {
        var result = FindingMatcher.Match(
            [Finding("src/A.cs", 10), Finding("src/A.cs", 20)],
            [Defect("src/A.cs", 21), Defect("src/A.cs", 11)]);

        Assert.Equal(2, result.Matched.Count);
        Assert.Empty(result.FalsePositives);
        // defect@21 should claim finding@20; defect@11 should claim finding@10.
        Assert.Contains(result.Matched, p => p.Expected.Line == 21 && p.Reported.Line == 20);
        Assert.Contains(result.Matched, p => p.Expected.Line == 11 && p.Reported.Line == 10);
    }

    // ---- Metrics ----

    [Fact]
    public void Metrics_compute_recall_precision_and_f1()
    {
        // 1 TP, 1 FP, 1 FN  →  recall .5, precision .5, f1 .5
        var match = new MatchResult(
            Matched: [new FindingPair(Finding("src/A.cs", 1), Defect("src/A.cs", 1))],
            FalsePositives: [Finding("src/A.cs", 9)],
            FalseNegatives: [Defect("src/A.cs", 80)]);

        var metrics = EvalMetrics.From([new CaseOutcome("c1", IsClean: false, match)]);

        Assert.Equal(1, metrics.TruePositives);
        Assert.Equal(1, metrics.FalsePositives);
        Assert.Equal(1, metrics.FalseNegatives);
        Assert.Equal(0.5, metrics.Recall, 3);
        Assert.Equal(0.5, metrics.Precision, 3);
        Assert.Equal(0.5, metrics.F1, 3);
    }

    [Fact]
    public void False_positive_rate_is_per_clean_case()
    {
        // Two clean cases, 3 spurious findings total → 1.5 FP per clean PR.
        var clean1 = new CaseOutcome("clean1", IsClean: true,
            new MatchResult([], [Finding("a", 1), Finding("a", 2)], []));
        var clean2 = new CaseOutcome("clean2", IsClean: true,
            new MatchResult([], [Finding("b", 1)], []));

        var metrics = EvalMetrics.From([clean1, clean2]);

        Assert.Equal(2, metrics.CleanCases);
        Assert.Equal(1.5, metrics.FalsePositiveRate, 3);
        Assert.Equal(0, metrics.Recall); // no defects to catch
    }

    [Fact]
    public void Severity_accuracy_counts_correct_severity_among_matched()
    {
        var match = new MatchResult(
            Matched:
            [
                new FindingPair(Finding("a", 1, severity: Severity.Major), Defect("a", 1, severity: "Major")),
                new FindingPair(Finding("b", 1, severity: Severity.Minor), Defect("b", 1, severity: "Blocker"))
            ],
            FalsePositives: [], FalseNegatives: []);

        var metrics = EvalMetrics.From([new CaseOutcome("c", false, match)]);

        Assert.Equal(0.5, metrics.SeverityAccuracy, 3); // 1 of 2 correct
    }

    [Fact]
    public void Empty_run_yields_zeroed_metrics_without_dividing_by_zero()
    {
        var metrics = EvalMetrics.From([]);

        Assert.Equal(0, metrics.Recall);
        Assert.Equal(0, metrics.Precision);
        Assert.Equal(0, metrics.F1);
        Assert.Equal(0, metrics.FalsePositiveRate);
    }
}
