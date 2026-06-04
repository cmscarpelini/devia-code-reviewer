using DevIa.Application.Reviews;
using DevIa.Evals.Caching;
using DevIa.Evals.Dataset;
using DevIa.Evals.Runner;
using DevIa.Infrastructure.Ai;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

var options = EvalCliOptions.Parse(args);

// Resolve the dataset: --dataset, else cwd/dataset, else next to the assembly (copied on build).
var datasetDir = options.DatasetPath
    ?? FirstExisting(
        Path.Combine(Directory.GetCurrentDirectory(), "dataset"),
        Path.Combine(AppContext.BaseDirectory, "dataset"))
    ?? Path.Combine(Directory.GetCurrentDirectory(), "dataset");

// Default reports alongside the resolved dataset, so running from anywhere keeps the report
// next to the cases it scored (instead of polluting the current directory).
var reportsDir = options.ReportsPath
    ?? Path.GetFullPath(Path.Combine(datasetDir, "..", "reports"));

var cases = DatasetLoader.Load(datasetDir);
if (cases.Count == 0)
{
    Console.Error.WriteLine($"No dataset cases found under '{datasetDir}'.");
    return 2;
}

var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
{
    Args = args,
    // Anchor config to the assembly so appsettings.json is found regardless of the cwd.
    ContentRootPath = AppContext.BaseDirectory
});
var configuration = builder.Configuration;
var provider = configuration["Llm:Provider"] ?? "GitHubModels";
var model = configuration["Llm:Model"] ?? "gpt-4o-mini";
var promptVersion = configuration["Llm:PromptVersion"] ?? "1";

if (options.Offline)
{
    // No LLM: a perfect oracle exercises the full path (matcher/metrics/report) deterministically.
    builder.Services.AddSingleton<IReviewPipeline>(new OfflineOraclePipeline(cases));
}
else
{
    builder.Services.AddReviewPipelineCore(configuration);
    if (options.Judge)
        builder.Services.AddSingleton<ISummaryJudge, SummaryJudge>();
}

using var host = builder.Build();

using var scope = host.Services.CreateScope();
var pipeline = scope.ServiceProvider.GetRequiredService<IReviewPipeline>();
var judge = options is { Offline: false, Judge: true }
    ? scope.ServiceProvider.GetRequiredService<ISummaryJudge>()
    : null;
var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Evals");

// Cache (opt-in): reuse LLM results per (prompt version, model, diff) across re-runs. Conflicts
// with multi-run variance — a cached run is identical — so warn when both are requested.
JsonFileResultCache? cache = null;
if (options.Cache && !options.Offline)
{
    var cacheDir = Path.GetFullPath(Path.Combine(datasetDir, "..", "cache"));
    cache = new JsonFileResultCache(cacheDir);
    pipeline = new CachingReviewPipeline(pipeline, cache, model, promptVersion);
    if (judge is not null)
        judge = new CachingSummaryJudge(judge, cache, model, promptVersion);
    if (options.Runs > 1)
        Console.WriteLine("WARNING: --cache makes every run identical (cache hit); variance will read as 0.\n");
}

var runner = new EvalRunner(pipeline, scope.ServiceProvider.GetRequiredService<ILogger<EvalRunner>>(), judge);

var label = options.Offline ? "offline oracle" : $"{provider}/{model}";
var cacheLabel = cache is not null ? $", cache=on, prompt v{promptVersion}" : "";
Console.WriteLine($"Running {cases.Count} eval case(s) × {options.Runs} run(s) [{label}{cacheLabel}]...\n");

// The LLM is non-deterministic even at temperature 0, so repeat the dataset and report the spread.
var runs = new List<EvalReport>();
for (var run = 1; run <= options.Runs; run++)
{
    if (options.Runs > 1)
        Console.WriteLine($"-- run {run}/{options.Runs}");
    runs.Add(await runner.RunAsync(
        cases, options.Offline ? "Offline" : provider, model, options.LineTolerance,
        TimeSpan.FromMilliseconds(options.DelayMs)));
}

if (cache is not null)
    Console.WriteLine($"\nCache: {cache.Hits} hit(s), {cache.Misses} miss(es).");

string reportPath;
double gateRecall, gateFpRate;
if (options.Runs == 1)
{
    PrintReport(runs[0]);
    reportPath = ReportWriter.Write(runs[0], reportsDir);
    gateRecall = runs[0].Metrics.Recall;
    gateFpRate = runs[0].Metrics.FalsePositiveRate;
}
else
{
    var aggregate = AggregateReport.From(runs);
    PrintAggregate(aggregate);
    reportPath = ReportWriter.Write(aggregate, reportsDir, "eval-agg");
    gateRecall = aggregate.Recall.Mean;
    gateFpRate = aggregate.FalsePositiveRate.Mean;
}

Console.WriteLine($"\nReport written to {reportPath}");

// Gates only enforce on real runs; the offline oracle is a plumbing smoke test.
if (options.Offline)
    return 0;

var gate = EvalGates.Evaluate(gateRecall, gateFpRate, new EvalGateOptions(options.MinRecall, options.MaxFalsePositiveRate));
if (gate.Passed)
{
    Console.WriteLine("\nGates: PASS");
    return 0;
}

Console.WriteLine("\nGates: FAIL");
foreach (var failure in gate.Failures)
    Console.WriteLine($"  - {failure}");
return 1;

static void PrintReport(EvalReport report)
{
    var m = report.Metrics;
    Console.WriteLine($"{"Case",-22} {"clean",-6} {"found",5} {"TP",4} {"FP",4} {"FN",4} {"judge",6}");
    Console.WriteLine(new string('-', 60));
    foreach (var c in report.Cases)
    {
        var judge = c.JudgeScore is { } s ? s.ToString("0.0") : "-";
        Console.WriteLine($"{Trunc(c.CaseId, 22),-22} {(c.IsClean ? "yes" : "no"),-6} {c.ReportedFindings,5} {c.TruePositives,4} {c.FalsePositives,4} {c.FalseNegatives,4} {judge,6}");
    }
    Console.WriteLine(new string('-', 60));
    Console.WriteLine($"Recall {m.Recall:P1}  Precision {m.Precision:P1}  F1 {m.F1:P1}");
    Console.WriteLine($"FP rate {m.FalsePositiveRate:0.00}/clean-PR  Severity acc {m.SeverityAccuracy:P1}");
    Console.WriteLine($"TP {m.TruePositives}  FP {m.FalsePositives}  FN {m.FalseNegatives}  (clean cases: {m.CleanCases}/{m.TotalCases})");
    if (report.AverageJudgeScore is { } avg)
        Console.WriteLine($"Avg summary judge score: {avg:0.0}/5");
}

static void PrintAggregate(AggregateReport a)
{
    Console.WriteLine($"{"Case",-22} {"clean",-6} {"detect%",8} {"clean%",7} {"avgTP",6} {"avgFP",6}");
    Console.WriteLine(new string('-', 64));
    foreach (var c in a.Cases)
    {
        var detect = c.IsClean ? "-" : c.FullDetectionRate.ToString("P0");
        var clean = c.IsClean ? c.CleanRate.ToString("P0") : "-";
        Console.WriteLine($"{Trunc(c.CaseId, 22),-22} {(c.IsClean ? "yes" : "no"),-6} {detect,8} {clean,7} {c.AvgTruePositives,6:0.0} {c.AvgFalsePositives,6:0.0}");
    }
    Console.WriteLine(new string('-', 64));
    Console.WriteLine($"(mean ± stddev over {a.Runs} runs)");
    Console.WriteLine($"Recall    {Fmt(a.Recall)}");
    Console.WriteLine($"Precision {Fmt(a.Precision)}");
    Console.WriteLine($"F1        {Fmt(a.F1)}");
    Console.WriteLine($"Severity  {Fmt(a.SeverityAccuracy)}");
    Console.WriteLine($"FP rate   {a.FalsePositiveRate.Mean:0.00} ± {a.FalsePositiveRate.StdDev:0.00} /clean-PR");
    if (a.Judge is { } judge)
        Console.WriteLine($"Judge     {judge.Mean:0.0} ± {judge.StdDev:0.0} /5");

    static string Fmt(DevIa.Evals.Scoring.MetricSummary s) =>
        $"{s.Mean:P1} ± {s.StdDev:P1}  [{s.Min:P0}–{s.Max:P0}]";
}

static string Trunc(string value, int max) => value.Length <= max ? value : value[..(max - 1)] + "…";

static string? FirstExisting(params string[] paths) => paths.FirstOrDefault(Directory.Exists);

/// <summary>Parsed CLI options for the eval runner.</summary>
internal sealed record EvalCliOptions(
    bool Offline,
    bool Judge,
    bool Cache,
    int Runs,
    int DelayMs,
    string? DatasetPath,
    string? ReportsPath,
    int LineTolerance,
    double MinRecall,
    double MaxFalsePositiveRate)
{
    public static EvalCliOptions Parse(string[] args)
    {
        bool offline = false, judge = false, cache = false;
        string? dataset = null, reports = null;
        var runs = 1;
        var delayMs = 0;
        var lineTolerance = 3;
        var minRecall = 0.7;
        var maxFpRate = 1.0;

        for (var i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--offline": offline = true; break;
                case "--judge": judge = true; break;
                case "--cache": cache = true; break;
                case "--runs": runs = Math.Max(1, int.Parse(Next(args, ref i))); break;
                case "--delay-ms": delayMs = Math.Max(0, int.Parse(Next(args, ref i))); break;
                case "--dataset": dataset = Next(args, ref i); break;
                case "--reports": reports = Next(args, ref i); break;
                case "--line-tolerance": lineTolerance = int.Parse(Next(args, ref i)); break;
                case "--min-recall": minRecall = double.Parse(Next(args, ref i)); break;
                case "--max-fp-rate": maxFpRate = double.Parse(Next(args, ref i)); break;
            }
        }

        return new EvalCliOptions(offline, judge, cache, runs, delayMs, dataset, reports, lineTolerance, minRecall, maxFpRate);
    }

    private static string Next(string[] args, ref int i) =>
        ++i < args.Length ? args[i] : throw new ArgumentException($"Missing value for '{args[i - 1]}'.");
}
