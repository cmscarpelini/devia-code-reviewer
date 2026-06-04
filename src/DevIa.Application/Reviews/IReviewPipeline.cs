using DevIa.Domain.Enums;

namespace DevIa.Application.Reviews;

/// <summary>Input to the review pipeline: the PR identity plus the (redacted) diff to analyze.</summary>
public sealed record ReviewPipelineInput(string RepoFullName, int PrNumber, string HeadSha, string Diff);

/// <summary>A finding produced by the pipeline, before it becomes a domain <c>Finding</c>.</summary>
public sealed record FindingDraft(
    Severity Severity,
    FindingCategory Category,
    string FilePath,
    int? Line,
    string Title,
    string Description,
    string? Suggestion);

/// <summary>
/// A single LLM exchange recorded for traceability and evals: which pipeline step produced it,
/// the model used, and the prompt content sent.
/// </summary>
public sealed record PromptTrace(string Step, string Model, string Content);

/// <summary>The structured assessment the pipeline returns for a review.</summary>
public sealed record ReviewAssessment(
    string Summary,
    int? RiskScore,
    string ModelProvider,
    string ModelVersion,
    int TokensUsed,
    IReadOnlyList<FindingDraft> Findings,
    IReadOnlyList<PromptTrace> Prompts,
    string RawResponse);

/// <summary>
/// Produces a structured assessment for a PR diff. Implemented by the Semantic Kernel
/// pipeline (provider-agnostic); the orchestration is explicit (see ADR-0004).
/// </summary>
public interface IReviewPipeline
{
    Task<ReviewAssessment> RunAsync(ReviewPipelineInput input, CancellationToken cancellationToken = default);
}
