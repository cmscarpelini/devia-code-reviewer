using System.Runtime.CompilerServices;
using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using DevIa.Infrastructure.Ai;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace DevIa.UnitTests.Reviews;

public class SemanticKernelReviewPipelineTests
{
    /// <summary>Returns canned responses in sequence (the last one repeats).</summary>
    private sealed class FakeChatCompletionService(params string[] responses) : IChatCompletionService
    {
        private int _index;
        public IReadOnlyDictionary<string, object?> Attributes { get; } = new Dictionary<string, object?>();

        public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, CancellationToken cancellationToken = default)
        {
            var response = responses[Math.Min(_index++, responses.Length - 1)];
            IReadOnlyList<ChatMessageContent> result = [new ChatMessageContent(AuthorRole.Assistant, response)];
            return Task.FromResult(result);
        }

        public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
            ChatHistory chatHistory, PromptExecutionSettings? executionSettings = null,
            Kernel? kernel = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            await Task.CompletedTask;
            yield return new StreamingChatMessageContent(AuthorRole.Assistant, responses[0]);
        }
    }

    private static SemanticKernelReviewPipeline Pipeline(params string[] responses)
        => new(
            new FakeChatCompletionService(responses),
            Options.Create(new LlmOptions { Provider = "OpenAI", Model = "gpt-4o-mini" }),
            NullLogger<SemanticKernelReviewPipeline>.Instance);

    private static ReviewPipelineInput Input() => new("acme/app", 7, "sha1", "some diff");

    [Fact]
    public async Task Parses_summary_and_findings()
    {
        const string json =
            """{"summary":"Refactors the user service.","findings":[{"title":"Null deref","severity":"Major","category":"Bug","file":"src/UserService.cs","line":42,"description":"Possible null dereference.","suggestion":"Add a guard."}]}""";

        var result = await Pipeline(json).RunAsync(Input());

        Assert.Equal("Refactors the user service.", result.Summary);
        Assert.Equal("gpt-4o-mini", result.ModelVersion);
        Assert.Equal(70, result.RiskScore); // Major
        var finding = Assert.Single(result.Findings);
        Assert.Equal(Severity.Major, finding.Severity);
        Assert.Equal(FindingCategory.Bug, finding.Category);
        Assert.Equal("src/UserService.cs", finding.FilePath);

        // The trace is captured for the result store: one analyze prompt + the raw model output.
        var prompt = Assert.Single(result.Prompts);
        Assert.Equal("analyze", prompt.Step);
        Assert.Equal("gpt-4o-mini", prompt.Model);
        Assert.Contains("DIFF:", prompt.Content);
        Assert.Equal(json, result.RawResponse);
    }

    [Fact]
    public async Task Skips_findings_missing_required_fields()
    {
        const string json = """{"summary":"x","findings":[{"severity":"Minor","category":"Style","file":"a.cs"}]}""";

        var result = await Pipeline(json).RunAsync(Input());

        Assert.Empty(result.Findings);
    }

    [Fact]
    public async Task Repairs_once_when_first_response_is_invalid_json()
    {
        const string valid = """{"summary":"ok","findings":[]}""";

        var result = await Pipeline("not json at all", valid).RunAsync(Input());

        Assert.Equal("ok", result.Summary);
        // Both the analyze and the repair prompts are traced; rawResponse is the repaired output.
        Assert.Equal(["analyze", "repair"], result.Prompts.Select(p => p.Step));
        Assert.Equal(valid, result.RawResponse);
    }

    [Fact]
    public async Task Throws_when_invalid_json_after_repair()
        => await Assert.ThrowsAsync<InvalidOperationException>(
            () => Pipeline("nope", "still nope").RunAsync(Input()));
}
