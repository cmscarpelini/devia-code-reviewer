using System.Text.Json;

namespace DevIa.Evals.Runner;

/// <summary>Writes an <see cref="EvalReport"/> as timestamped JSON for trend tracking.</summary>
public static class ReportWriter
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string Write<T>(T report, string reportsDirectory, string prefix = "eval")
    {
        Directory.CreateDirectory(reportsDirectory);
        var fileName = $"{prefix}-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}.json";
        var path = Path.Combine(reportsDirectory, fileName);
        File.WriteAllText(path, JsonSerializer.Serialize(report, JsonOptions));
        return path;
    }
}
