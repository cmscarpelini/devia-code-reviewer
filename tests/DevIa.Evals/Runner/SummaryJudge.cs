using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace DevIa.Evals.Runner;

/// <summary>Scores a review summary against the diff on a 1–5 rubric (LLM-as-judge).</summary>
public interface ISummaryJudge
{
    Task<double?> ScoreAsync(string diff, string summary, CancellationToken cancellationToken = default);
}

/// <summary>
/// LLM-as-judge for the open-ended executive summary (ADR-0005): a second model scores the
/// summary against a fixed rubric (1–5) on accuracy, completeness, and conciseness, returning
/// their average. A trend signal, not a hard gate.
/// </summary>
public sealed class SummaryJudge(IChatCompletionService chat) : ISummaryJudge
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<double?> ScoreAsync(string diff, string summary, CancellationToken cancellationToken = default)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(
            "You are an impartial judge of code-review summaries. Score the summary against the diff " +
            "on a 1-5 scale for each criterion. Respond with JSON only.");
        history.AddUserMessage(
            $$"""
            Rate this review summary on three criteria, each 1 (poor) to 5 (excellent):
            - accuracy: does it correctly describe the diff?
            - completeness: does it cover the important changes?
            - conciseness: is it focused, without filler?

            Return ONLY JSON: { "accuracy": 0, "completeness": 0, "conciseness": 0 }

            DIFF:
            {{diff}}

            SUMMARY:
            {{summary}}
            """);

        var settings = new OpenAIPromptExecutionSettings { Temperature = 0, ResponseFormat = "json_object" };
        var result = await chat.GetChatMessageContentAsync(history, settings, kernel: null, cancellationToken);

        var scores = TryParse(result.Content);
        if (scores is null)
            return null;

        return (Clamp(scores.Accuracy) + Clamp(scores.Completeness) + Clamp(scores.Conciseness)) / 3.0;
    }

    private static RubricScores? TryParse(string? content)
    {
        if (string.IsNullOrWhiteSpace(content))
            return null;
        try
        {
            return JsonSerializer.Deserialize<RubricScores>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static double Clamp(int score) => Math.Clamp(score, 1, 5);

    private sealed record RubricScores(
        [property: JsonPropertyName("accuracy")] int Accuracy,
        [property: JsonPropertyName("completeness")] int Completeness,
        [property: JsonPropertyName("conciseness")] int Conciseness);
}
