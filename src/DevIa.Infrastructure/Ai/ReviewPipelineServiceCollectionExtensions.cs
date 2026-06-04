using DevIa.Application.Reviews;
using DevIa.Infrastructure.GitHub;
using DevIa.Infrastructure.Reviews;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using MongoDB.Driver;

namespace DevIa.Infrastructure.Ai;

public static class ReviewPipelineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the review pipeline: the chat completion service (provider chosen by the
    /// "Llm" config section — the only provider-aware seam), the pipeline, the diff source,
    /// the result store, and the <see cref="ProcessReviewJob"/> orchestrator.
    /// </summary>
    public static IServiceCollection AddReviewPipeline(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddReviewPipelineCore(configuration);

        services.AddHttpClient<IDiffSource, GitHubDiffSource>(client =>
            client.DefaultRequestHeaders.UserAgent.ParseAdd("DevIa-CodeReviewer"));

        // Raw review results live in MongoDB (collection review_results). The client is thread-safe
        // and pools connections, so it is a singleton; the store reads the collection per request.
        services.Configure<MongoOptions>(configuration.GetSection("Mongo"));
        services.AddSingleton<IMongoClient>(sp =>
            new MongoClient(sp.GetRequiredService<IOptions<MongoOptions>>().Value.ConnectionString));
        services.AddSingleton(sp =>
            sp.GetRequiredService<IMongoClient>().GetDatabase(
                sp.GetRequiredService<IOptions<MongoOptions>>().Value.Database));
        services.AddScoped<IReviewResultStore, MongoReviewResultStore>();
        services.AddScoped<ProcessReviewJob>();

        return services;
    }

    /// <summary>
    /// Registers only the provider-agnostic core needed to produce an assessment: the chat
    /// completion service (provider chosen by the "Llm" config section) and the pipeline. Shared
    /// by the Worker (full pipeline) and the eval harness (which needs just <see cref="IReviewPipeline"/>).
    /// </summary>
    public static IServiceCollection AddReviewPipelineCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<LlmOptions>(configuration.GetSection("Llm"));
        var llm = configuration.GetSection("Llm").Get<LlmOptions>() ?? new LlmOptions();

        switch (llm.Provider)
        {
            case "AzureOpenAI":
                services.AddAzureOpenAIChatCompletion(
                    deploymentName: llm.Model,
                    endpoint: llm.Endpoint ?? throw new InvalidOperationException("Llm:Endpoint is required for AzureOpenAI."),
                    apiKey: llm.ApiKey);
                break;
            case "GitHubModels":
                services.AddOpenAIChatCompletion(
                    modelId: llm.Model,
                    endpoint: new Uri(llm.Endpoint ?? "https://models.inference.ai.azure.com"),
                    apiKey: llm.ApiKey);
                break;
            case "Ollama":
                services.AddOpenAIChatCompletion(
                    modelId: llm.Model,
                    endpoint: new Uri(llm.Endpoint ?? "http://localhost:11434/v1"),
                    apiKey: "ollama");
                break;
            default: // OpenAI
                services.AddOpenAIChatCompletion(modelId: llm.Model, apiKey: llm.ApiKey);
                break;
        }

        services.AddScoped<IReviewPipeline, SemanticKernelReviewPipeline>();
        return services;
    }
}
