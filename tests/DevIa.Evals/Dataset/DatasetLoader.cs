using System.Text.Json;

namespace DevIa.Evals.Dataset;

/// <summary>
/// Loads golden-dataset cases from disk. Each case is a folder containing <c>diff.patch</c>,
/// <c>expected.json</c>, and an optional <c>meta.json</c>; the folder name is the case id.
/// </summary>
public static class DatasetLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static IReadOnlyList<EvalCase> Load(string datasetDirectory)
    {
        if (!Directory.Exists(datasetDirectory))
            throw new DirectoryNotFoundException($"Dataset directory not found: {datasetDirectory}");

        var cases = new List<EvalCase>();

        foreach (var dir in Directory.EnumerateDirectories(datasetDirectory).OrderBy(d => d))
        {
            var diffPath = Path.Combine(dir, "diff.patch");
            var expectedPath = Path.Combine(dir, "expected.json");
            if (!File.Exists(diffPath) || !File.Exists(expectedPath))
                continue; // not a case folder

            var diff = File.ReadAllText(diffPath);
            var expected = JsonSerializer.Deserialize<ExpectedResult>(File.ReadAllText(expectedPath), JsonOptions)
                ?? throw new InvalidOperationException($"Invalid expected.json in {dir}");

            var metaPath = Path.Combine(dir, "meta.json");
            var meta = File.Exists(metaPath)
                ? JsonSerializer.Deserialize<CaseMeta>(File.ReadAllText(metaPath), JsonOptions)
                : null;

            cases.Add(new EvalCase(Path.GetFileName(dir), diff, expected, meta));
        }

        return cases;
    }
}
