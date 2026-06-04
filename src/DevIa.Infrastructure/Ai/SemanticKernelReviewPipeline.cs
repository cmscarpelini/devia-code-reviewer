using System.Text.Json;
using System.Text.Json.Serialization;
using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.OpenAI;

namespace DevIa.Infrastructure.Ai;

/// <summary>
/// Provider-agnostic review pipeline (ADR-0004). Explicitly orchestrated: it prompts the LLM
/// for a structured JSON assessment, validates it, and repairs once if the output is invalid.
/// </summary>
public sealed class SemanticKernelReviewPipeline(
    IChatCompletionService chat,
    IOptions<LlmOptions> options,
    ILogger<SemanticKernelReviewPipeline> logger) : IReviewPipeline
{
    private const string SystemPrompt =
        "You are a senior code reviewer. Analyze the diff and report only real problems; " +
        "do not invent findings. Respond with JSON only, no prose.";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly LlmOptions _options = options.Value;

    public async Task<ReviewAssessment> RunAsync(ReviewPipelineInput input, CancellationToken cancellationToken = default)
    {
        // Record every prompt and the final raw model output so the result store keeps a full,
        // replayable trace (used for debugging and evals — see data-model.md / ADR-0005).
        var prompts = new List<PromptTrace>();

        var analyzePrompt = UserPrompt(input.Diff);
        prompts.Add(new PromptTrace("analyze", _options.Model, analyzePrompt));
        var (content, tokens) = await CompleteAsync(analyzePrompt, cancellationToken);
        var rawResponse = content;

        var dto = TryParse(content);
        if (dto is null)
        {
            logger.LogWarning("LLM output was not valid JSON; attempting one repair.");
            var repairPrompt = RepairPrompt(content);
            prompts.Add(new PromptTrace("repair", _options.Model, repairPrompt));
            var (repaired, _) = await CompleteAsync(repairPrompt, cancellationToken);
            rawResponse = repaired;
            dto = TryParse(repaired);
        }

        if (dto is null)
            throw new InvalidOperationException("LLM returned invalid JSON after one repair attempt.");

        var findings = (dto.Findings ?? [])
            .Where(f => !string.IsNullOrWhiteSpace(f.Title)
                        && !string.IsNullOrWhiteSpace(f.Description)
                        && !string.IsNullOrWhiteSpace(f.File))
            .Select(f => new FindingDraft(
                ParseSeverity(f.Severity), ParseCategory(f.Category),
                f.File!, f.Line, f.Title!, f.Description!, f.Suggestion))
            .ToList();

        var summary = string.IsNullOrWhiteSpace(dto.Summary) ? "No summary produced." : dto.Summary!;

        return new ReviewAssessment(
            Summary: summary,
            RiskScore: RiskFrom(findings),
            ModelProvider: _options.Provider,
            ModelVersion: _options.Model,
            TokensUsed: tokens,
            Findings: findings,
            Prompts: prompts,
            RawResponse: rawResponse);
    }

    private async Task<(string content, int tokens)> CompleteAsync(string userMessage, CancellationToken cancellationToken)
    {
        var history = new ChatHistory();
        history.AddSystemMessage(SystemPrompt);
        history.AddUserMessage(userMessage);

        var settings = new OpenAIPromptExecutionSettings
        {
            Temperature = 0,
            ResponseFormat = "json_object"
        };

        var result = await chat.GetChatMessageContentAsync(history, settings, kernel: null, cancellationToken);
        return (result.Content ?? string.Empty, ExtractTokens(result));
    }

    private static string UserPrompt(string diff) =>
        $$"""
        Review the following diff. Return ONLY valid JSON of this exact shape:
        {
          "summary": "concise executive summary of the changes",
          "findings": [
            { "title": "", "severity": "Blocker|Major|Minor|Info",
              "category": "Bug|Security|Style|Performance|Test",
              "file": "", "line": 0, "description": "", "suggestion": "" }
          ]
        }

        DIFF:
        {{diff}}
        """;

    private static string RepairPrompt(string invalid) =>
        "Your previous response was not valid JSON for the required schema. " +
        "Return ONLY valid JSON for that schema, nothing else. Previous response:\n" + invalid;

    private static PipelineDto? TryParse(string content)
    {
        try
        {
            return JsonSerializer.Deserialize<PipelineDto>(content, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static Severity ParseSeverity(string? value) =>
        Enum.TryParse<Severity>(value, ignoreCase: true, out var v) ? v : Severity.Minor;

    private static FindingCategory ParseCategory(string? value) =>
        Enum.TryParse<FindingCategory>(value, ignoreCase: true, out var v) ? v : FindingCategory.Bug;

    private static int? RiskFrom(IReadOnlyList<FindingDraft> findings) =>
        findings.Count == 0
            ? 0
            : findings.Max(f => f.Severity switch
            {
                Severity.Blocker => 100,
                Severity.Major => 70,
                Severity.Minor => 40,
                _ => 10
            });

    private static int ExtractTokens(ChatMessageContent result)
    {
        try
        {
            if (result.Metadata is not null
                && result.Metadata.TryGetValue("Usage", out var usage)
                && usage is not null)
            {
                var property = usage.GetType().GetProperty("TotalTokenCount");
                if (property?.GetValue(usage) is { } value)
                    return Convert.ToInt32(value);
            }
        }
        catch
        {
            // best-effort token accounting; ignore.
        }
        return 0;
    }

    private sealed class PipelineDto
    {
        [JsonPropertyName("summary")] public string? Summary { get; set; }
        [JsonPropertyName("findings")] public List<FindingDto>? Findings { get; set; }
    }

    private sealed class FindingDto
    {
        [JsonPropertyName("title")] public string? Title { get; set; }
        [JsonPropertyName("severity")] public string? Severity { get; set; }
        [JsonPropertyName("category")] public string? Category { get; set; }
        [JsonPropertyName("file")] public string? File { get; set; }
        [JsonPropertyName("line")] public int? Line { get; set; }
        [JsonPropertyName("description")] public string? Description { get; set; }
        [JsonPropertyName("suggestion")] public string? Suggestion { get; set; }
    }
}
