using DevIa.Application.Reviews;
using DevIa.Evals.Dataset;

namespace DevIa.Evals.Scoring;

/// <summary>A reported finding matched to a ground-truth defect (a true positive).</summary>
public sealed record FindingPair(FindingDraft Reported, ExpectedDefect Expected);

/// <summary>
/// The outcome of comparing one case's reported findings against its ground truth:
/// matched pairs (TP), unmatched reported findings (FP), and unmatched defects (FN).
/// </summary>
public sealed record MatchResult(
    IReadOnlyList<FindingPair> Matched,
    IReadOnlyList<FindingDraft> FalsePositives,
    IReadOnlyList<ExpectedDefect> FalseNegatives);

/// <summary>
/// Links reported findings to ground-truth defects by <b>same file + nearby line</b> (ADR-0005).
/// Greedy: each defect claims the closest still-unmatched finding. Matching is style-agnostic so
/// the harness is fair across models: file paths are compared by basename (models differ on how
/// much of the path they include) and category is NOT a match criterion (models disagree on
/// classification, e.g. Bug vs Style for the same issue) — category correctness is measured
/// separately as an accuracy metric, not as a reason to reject a real match.
/// </summary>
public static class FindingMatcher
{
    public static MatchResult Match(
        IReadOnlyList<FindingDraft> reported, IReadOnlyList<ExpectedDefect> expected, int lineTolerance = 3)
    {
        var remaining = reported.ToList();
        var matched = new List<FindingPair>();
        var falseNegatives = new List<ExpectedDefect>();

        foreach (var defect in expected)
        {
            FindingDraft? best = null;
            var bestDistance = int.MaxValue;

            foreach (var finding in remaining)
            {
                if (!IsCompatible(finding, defect, lineTolerance, out var distance))
                    continue;
                if (distance < bestDistance)
                {
                    best = finding;
                    bestDistance = distance;
                }
            }

            if (best is not null)
            {
                matched.Add(new FindingPair(best, defect));
                remaining.Remove(best);
            }
            else
            {
                falseNegatives.Add(defect);
            }
        }

        return new MatchResult(matched, remaining, falseNegatives);
    }

    private static bool IsCompatible(FindingDraft finding, ExpectedDefect defect, int lineTolerance, out int distance)
    {
        distance = 0;

        if (!SameFile(finding.FilePath, defect.File))
            return false;

        if (defect.Line is { } expectedLine)
        {
            // A line-specific defect needs a line-located finding within tolerance.
            if (finding.Line is not { } reportedLine)
                return false;
            distance = Math.Abs(reportedLine - expectedLine);
            if (distance > lineTolerance)
                return false;
        }

        return true;
    }

    // Equal when normalized, or when the basenames match — so "src/A.cs" (one model) and "A.cs"
    // (another) are the same file. Same-basename collisions across directories are a non-issue for
    // the small, focused golden-dataset cases.
    private static bool SameFile(string a, string b)
    {
        var na = Normalize(a);
        var nb = Normalize(b);
        return string.Equals(na, nb, StringComparison.OrdinalIgnoreCase)
            || string.Equals(Basename(na), Basename(nb), StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string path) => path.Trim().Replace('\\', '/');

    private static string Basename(string path)
    {
        var slash = path.LastIndexOf('/');
        return slash >= 0 ? path[(slash + 1)..] : path;
    }
}
