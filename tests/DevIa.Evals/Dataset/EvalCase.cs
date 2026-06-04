using System.Text.Json.Serialization;

namespace DevIa.Evals.Dataset;

/// <summary>A ground-truth defect labeled in a golden-dataset case (<c>expected.json</c>).</summary>
public sealed record ExpectedDefect(
    [property: JsonPropertyName("file")] string File,
    [property: JsonPropertyName("line")] int? Line,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("severity")] string Severity,
    [property: JsonPropertyName("description")] string? Description);

/// <summary>Ground truth for a case: the labeled defects and whether any finding is expected.</summary>
public sealed record ExpectedResult(
    [property: JsonPropertyName("defects")] IReadOnlyList<ExpectedDefect> Defects,
    [property: JsonPropertyName("shouldHaveFindings")] bool ShouldHaveFindings);

/// <summary>Optional descriptive metadata for a case (<c>meta.json</c>).</summary>
public sealed record CaseMeta(
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("size")] string? Size,
    [property: JsonPropertyName("tags")] string[]? Tags);

/// <summary>
/// One golden-dataset case: a PR diff plus its labeled ground truth. A "clean" case
/// (<see cref="ExpectedResult.ShouldHaveFindings"/> false) carries no defects and is used to
/// measure the false-positive rate.
/// </summary>
public sealed record EvalCase(string Id, string Diff, ExpectedResult Expected, CaseMeta? Meta)
{
    public bool IsClean => !Expected.ShouldHaveFindings;
}
