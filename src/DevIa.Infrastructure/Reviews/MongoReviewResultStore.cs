using DevIa.Application.Reviews;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace DevIa.Infrastructure.Reviews;

/// <summary>
/// Persists the raw review result (diff + full findings + model metadata) in MongoDB and returns
/// the generated document id, which the review stores as <c>RawResultRef</c>. Per ADR-0003 this
/// write happens before the Postgres update, so the heavy payload is durable even if the relational
/// commit later fails (the job retries idempotently).
/// </summary>
public sealed class MongoReviewResultStore : IReviewResultStore
{
    private readonly IMongoCollection<ReviewResultDocument> _collection;

    public MongoReviewResultStore(IMongoDatabase database, IOptions<MongoOptions> options)
    {
        _collection = database.GetCollection<ReviewResultDocument>(options.Value.ResultsCollection);
    }

    public async Task<string> SaveAsync(
        Guid reviewId, ReviewPipelineInput input, ReviewAssessment assessment, CancellationToken cancellationToken = default)
    {
        var document = new ReviewResultDocument
        {
            ReviewId = reviewId,
            PullRequest = new ReviewResultDocument.PullRequestRef
            {
                RepositoryFullName = input.RepoFullName,
                Number = input.PrNumber,
                HeadSha = input.HeadSha
            },
            Diff = input.Diff,
            Prompts = assessment.Prompts.Select(p => new ReviewResultDocument.PromptDoc
            {
                Step = p.Step,
                Model = p.Model,
                Content = p.Content
            }).ToList(),
            RawResponse = assessment.RawResponse,
            Summary = assessment.Summary,
            RiskScore = assessment.RiskScore,
            TokensUsed = assessment.TokensUsed,
            ModelProvider = assessment.ModelProvider,
            ModelVersion = assessment.ModelVersion,
            CreatedAt = DateTime.UtcNow,
            Findings = assessment.Findings.Select(f => new ReviewResultDocument.FindingDoc
            {
                Title = f.Title,
                Severity = f.Severity.ToString(),
                Category = f.Category.ToString(),
                File = f.FilePath,
                Line = f.Line,
                Description = f.Description,
                Suggestion = f.Suggestion
            }).ToList()
        };

        await _collection.InsertOneAsync(document, options: null, cancellationToken);

        return document.Id.ToString();
    }
}
