using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace DevIa.Infrastructure.Reviews;

/// <summary>
/// The raw, high-volume review result persisted in MongoDB (collection <c>review_results</c>,
/// one document per <see cref="Domain.Reviews.Review"/>). The generated <see cref="Id"/> is
/// returned to the caller and stored on the review as <c>RawResultRef</c>, linking Postgres ↔ Mongo.
/// Matches docs/domain/data-model.md.
/// </summary>
public sealed class ReviewResultDocument
{
    [BsonId]
    public ObjectId Id { get; set; }

    [BsonElement("reviewId")]
    [BsonRepresentation(BsonType.String)]
    public Guid ReviewId { get; set; }

    [BsonElement("pullRequest")]
    public PullRequestRef PullRequest { get; set; } = new();

    [BsonElement("diff")]
    public string Diff { get; set; } = string.Empty;

    [BsonElement("prompts")]
    public List<PromptDoc> Prompts { get; set; } = [];

    [BsonElement("rawResponse")]
    public string RawResponse { get; set; } = string.Empty;

    [BsonElement("summary")]
    public string Summary { get; set; } = string.Empty;

    [BsonElement("riskScore")]
    [BsonIgnoreIfNull]
    public int? RiskScore { get; set; }

    [BsonElement("findings")]
    public List<FindingDoc> Findings { get; set; } = [];

    [BsonElement("tokensUsed")]
    public int TokensUsed { get; set; }

    [BsonElement("modelProvider")]
    public string ModelProvider { get; set; } = string.Empty;

    [BsonElement("modelVersion")]
    public string ModelVersion { get; set; } = string.Empty;

    [BsonElement("createdAt")]
    public DateTime CreatedAt { get; set; }

    public sealed class PullRequestRef
    {
        [BsonElement("repositoryFullName")]
        public string RepositoryFullName { get; set; } = string.Empty;

        [BsonElement("number")]
        public int Number { get; set; }

        [BsonElement("headSha")]
        public string HeadSha { get; set; } = string.Empty;
    }

    public sealed class PromptDoc
    {
        [BsonElement("step")]
        public string Step { get; set; } = string.Empty;

        [BsonElement("model")]
        public string Model { get; set; } = string.Empty;

        [BsonElement("content")]
        public string Content { get; set; } = string.Empty;
    }

    public sealed class FindingDoc
    {
        [BsonElement("title")]
        public string Title { get; set; } = string.Empty;

        [BsonElement("severity")]
        public string Severity { get; set; } = string.Empty;

        [BsonElement("category")]
        public string Category { get; set; } = string.Empty;

        [BsonElement("file")]
        public string File { get; set; } = string.Empty;

        [BsonElement("line")]
        [BsonIgnoreIfNull]
        public int? Line { get; set; }

        [BsonElement("description")]
        public string Description { get; set; } = string.Empty;

        [BsonElement("suggestion")]
        [BsonIgnoreIfNull]
        public string? Suggestion { get; set; }
    }
}
