namespace DevIa.Infrastructure.Reviews;

/// <summary>
/// MongoDB settings (config section "Mongo") for the raw review-result store. The connection
/// string and database are required; the collection name defaults to <c>review_results</c>.
/// </summary>
public sealed class MongoOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public string Database { get; set; } = "devia";

    public string ResultsCollection { get; set; } = "review_results";
}
