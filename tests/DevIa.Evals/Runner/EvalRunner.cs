using System.Net;
using DevIa.Application.Reviews;
using DevIa.Evals.Dataset;
using DevIa.Evals.Scoring;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace DevIa.Evals.Runner;

/// <summary>
/// Runs the real review pipeline over the golden dataset and scores the results: matches findings
/// to ground truth (recall/precision/F1/FP/severity) and optionally judges each summary. The
/// pipeline is the product's actual <see cref="IReviewPipeline"/> — this measures AI quality, not code.
/// Resilient to provider rate limits (HTTP 429) via exponential backoff.
/// </summary>
public sealed class EvalRunner(IReviewPipeline pipeline, ILogger<EvalRunner> logger, ISummaryJudge? judge = null)
{
    // Backoff schedule (seconds) for HTTP 429s — eval runs burst many calls at free-tier providers.
    private static readonly int[] RetryDelaysSeconds = [2, 5, 12, 30, 60];

    public async Task<EvalReport> RunAsync(
        IReadOnlyList<EvalCase> cases, string provider, string model, int lineTolerance,
        TimeSpan delayBetweenCalls = default, CancellationToken cancellationToken = default)
    {
        var outcomes = new List<CaseOutcome>();
        var caseReports = new List<CaseReport>();
        var judgeScores = new List<double>();

        foreach (var evalCase in cases)
        {
            logger.LogInformation("Evaluating case {CaseId}...", evalCase.Id);

            // The pipeline only needs the diff; identity fields are placeholders for eval runs.
            var input = new ReviewPipelineInput($"eval/{evalCase.Id}", PrNumber: 0, HeadSha: "eval", evalCase.Diff);
            var assessment = await WithRetryAsync(() => pipeline.RunAsync(input, cancellationToken), cancellationToken);

            var match = FindingMatcher.Match(assessment.Findings, evalCase.Expected.Defects, lineTolerance);
            outcomes.Add(new CaseOutcome(evalCase.Id, evalCase.IsClean, match));

            double? judgeScore = null;
            if (judge is not null && !evalCase.IsClean)
            {
                await DelayAsync(delayBetweenCalls, cancellationToken);
                judgeScore = await WithRetryAsync(
                    () => judge.ScoreAsync(evalCase.Diff, assessment.Summary, cancellationToken), cancellationToken);
                if (judgeScore is { } score)
                    judgeScores.Add(score);
            }

            caseReports.Add(new CaseReport(
                evalCase.Id, evalCase.IsClean, assessment.Findings.Count,
                match.Matched.Count, match.FalsePositives.Count, match.FalseNegatives.Count,
                judgeScore, assessment.Summary, Describe(match)));

            await DelayAsync(delayBetweenCalls, cancellationToken);
        }

        var metrics = EvalMetrics.From(outcomes);
        var averageJudge = judgeScores.Count == 0 ? (double?)null : judgeScores.Average();

        return new EvalReport(DateTimeOffset.UtcNow, provider, model, metrics, averageJudge, caseReports);
    }

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken)
    {
        for (var attempt = 0; ; attempt++)
        {
            try
            {
                return await action();
            }
            catch (HttpOperationException ex)
                when (ex.StatusCode == HttpStatusCode.TooManyRequests && attempt < RetryDelaysSeconds.Length)
            {
                var seconds = RetryDelaysSeconds[attempt];
                logger.LogWarning("Rate limited (429); backing off {Seconds}s (attempt {Attempt}/{Max}).",
                    seconds, attempt + 1, RetryDelaysSeconds.Length);
                await Task.Delay(TimeSpan.FromSeconds(seconds), cancellationToken);
            }
        }
    }

    private static Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        delay > TimeSpan.Zero ? Task.Delay(delay, cancellationToken) : Task.CompletedTask;

    // Flatten a match into per-finding rows tagged TP/FP, with the expected severity for TPs.
    private static List<ReportedFinding> Describe(MatchResult match)
    {
        var rows = match.Matched.Select(pair => new ReportedFinding(
                pair.Reported.FilePath, pair.Reported.Line, pair.Reported.Severity.ToString(),
                pair.Reported.Category.ToString(), pair.Reported.Title, "TP", pair.Expected.Severity))
            .ToList();

        rows.AddRange(match.FalsePositives.Select(f => new ReportedFinding(
            f.FilePath, f.Line, f.Severity.ToString(), f.Category.ToString(), f.Title, "FP", null)));

        return rows;
    }
}
