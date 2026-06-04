using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using DevIa.Application.Identity;
using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using DevIa.Infrastructure.Persistence;
using DevIa.Infrastructure.Reviews;
using DevIa.Worker;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;

namespace DevIa.IntegrationTests;

/// <summary>
/// End-to-end async flow (SPEC-0001, slice B2): a signed webhook creates a Pending review,
/// the publisher enqueues it, and the Worker consumer moves it to Processing — all against
/// real Postgres and RabbitMQ containers.
/// </summary>
public sealed class WebhookFlowTests : IAsyncLifetime
{
    private const string WebhookSecret = "test-secret";

    private readonly ConcurrentQueue<string> _logs = new();

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:17")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3-management")
        .WithUsername("devia")
        .WithPassword("devia")
        .Build();

    private readonly MongoDbContainer _mongo = new MongoDbBuilder("mongo:7")
        .Build();

    private const string MongoDatabase = "devia_test";

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _rabbitMq.StartAsync();
        await _mongo.StartAsync();
    }

    public async Task DisposeAsync()
    {
        await _postgres.DisposeAsync();
        await _rabbitMq.DisposeAsync();
        await _mongo.DisposeAsync();
    }

    [Fact]
    public async Task Signed_webhook_runs_pipeline_to_awaiting_human_review()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient(); // builds + starts the host (and the consumer)

        const string headSha = "sha-int-1";
        var response = await PostWebhookAsync(client, headSha);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        // The worker consumes asynchronously, runs the (faked) pipeline, and completes the
        // assessment — poll until the review reaches AwaitingHumanReview.
        var status = await WaitForStatusAsync(factory, headSha, ReviewStatus.AwaitingHumanReview, TimeSpan.FromSeconds(30));
        Assert.True(status == ReviewStatus.AwaitingHumanReview,
            $"Expected AwaitingHumanReview but was {status}. Worker logs:\n{string.Join("\n", _logs)}");
    }

    [Fact]
    public async Task Pipeline_persists_the_raw_result_to_mongo_and_links_it_to_the_review()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient();

        const string headSha = "sha-int-mongo";
        await PostWebhookAsync(client, headSha);

        var status = await WaitForStatusAsync(factory, headSha, ReviewStatus.AwaitingHumanReview, TimeSpan.FromSeconds(30));
        Assert.True(status == ReviewStatus.AwaitingHumanReview,
            $"Expected AwaitingHumanReview but was {status}. Worker logs:\n{string.Join("\n", _logs)}");

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevIaDbContext>();
        var review = await db.Reviews.AsNoTracking().FirstAsync(r => r.HeadSha == headSha);

        // The review now references the Mongo document by its ObjectId string.
        Assert.False(string.IsNullOrWhiteSpace(review.RawResultRef));

        var database = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
        var collection = database.GetCollection<ReviewResultDocument>(
            scope.ServiceProvider.GetRequiredService<IOptions<MongoOptions>>().Value.ResultsCollection);
        var document = await collection.Find(d => d.ReviewId == review.Id).FirstOrDefaultAsync();

        Assert.NotNull(document);
        Assert.Equal(review.RawResultRef, document!.Id.ToString());
        Assert.Equal("acme/app", document.PullRequest.RepositoryFullName);
        Assert.Equal(headSha, document.PullRequest.HeadSha);
        Assert.Equal("Looks fine.", document.Summary);
        Assert.Equal("Fake", document.ModelProvider);
        Assert.Single(document.Findings);
        Assert.Equal("Nit", document.Findings[0].Title);
        Assert.False(string.IsNullOrEmpty(document.Diff));

        // The full LLM trace is persisted too (prompts per step + the raw model output).
        Assert.Single(document.Prompts);
        Assert.Equal("analyze", document.Prompts[0].Step);
        Assert.Equal("test", document.Prompts[0].Model);
        Assert.False(string.IsNullOrEmpty(document.Prompts[0].Content));
        Assert.Contains("Looks fine.", document.RawResponse);
    }

    [Fact]
    public async Task Verdict_endpoint_approves_a_reviewed_pr()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient();

        const string headSha = "sha-int-verdict";
        await PostWebhookAsync(client, headSha);

        var status = await WaitForStatusAsync(factory, headSha, ReviewStatus.AwaitingHumanReview, TimeSpan.FromSeconds(30));
        Assert.True(status == ReviewStatus.AwaitingHumanReview,
            $"Expected AwaitingHumanReview but was {status}. Worker logs:\n{string.Join("\n", _logs)}");

        var reviewId = await GetReviewIdAsync(factory, headSha);

        var token = await GetTokenAsync(client, "reviewer-code");
        var verdictResponse = await PostVerdictAsync(client, reviewId, token, "Approved");
        Assert.Equal(HttpStatusCode.OK, verdictResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevIaDbContext>();
        var review = await db.Reviews.AsNoTracking().FirstAsync(r => r.Id == reviewId);
        Assert.Equal(ReviewStatus.Approved, review.Status);
        Assert.True(await db.Verdicts.AsNoTracking().AnyAsync(v => v.ReviewId == reviewId), "verdict row missing");
        Assert.True(await db.AuditLogs.AsNoTracking().AnyAsync(a => a.EntityId == reviewId), "audit row missing");
    }

    [Fact]
    public async Task GitHub_callback_issues_a_token()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient();

        var token = await GetTokenAsync(client, "reviewer-code");

        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Verdict_without_a_token_is_unauthorized()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync(
            $"/reviews/{Guid.NewGuid()}/verdict", new { decision = "Approved", justification = (string?)null });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Developer_role_cannot_record_a_verdict()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient();

        var token = await GetTokenAsync(client, "developer-code"); // login not in ReviewerLogins
        var response = await PostVerdictAsync(client, Guid.NewGuid(), token, "Approved");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Review_queue_and_detail_are_served_to_an_authenticated_user()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient();

        const string headSha = "sha-int-read";
        await PostWebhookAsync(client, headSha);
        var status = await WaitForStatusAsync(factory, headSha, ReviewStatus.AwaitingHumanReview, TimeSpan.FromSeconds(30));
        Assert.Equal(ReviewStatus.AwaitingHumanReview, status);

        var reviewId = await GetReviewIdAsync(factory, headSha);
        var token = await GetTokenAsync(client, "reviewer-code");

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/reviews?status=AwaitingHumanReview");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var items = await listResponse.Content.ReadFromJsonAsync<List<ReviewListDto>>();
        Assert.Contains(items!, i => i.Id == reviewId && i.RepositoryFullName == "acme/app" && i.Status == "AwaitingHumanReview");

        var detailRequest = new HttpRequestMessage(HttpMethod.Get, $"/reviews/{reviewId}");
        detailRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var detailResponse = await client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ReviewDetailDto>();
        Assert.Equal("Looks fine.", detail!.Summary);
        Assert.NotEmpty(detail.Findings);
    }

    [Fact]
    public async Task Installation_connects_repositories_and_can_be_deactivated()
    {
        await using var factory = CreateFactory();
        await MigrateAsync(factory);
        var client = factory.CreateClient();

        const string installation = """{"action":"created","installation":{"id":999,"account":{"id":100,"login":"acme","type":"Organization"}},"repositories":[{"id":555,"name":"svc","full_name":"acme/svc"}]}""";
        var installResponse = await PostInstallationAsync(client, installation);
        Assert.Equal(HttpStatusCode.Accepted, installResponse.StatusCode);

        var token = await GetTokenAsync(client, "reviewer-code");

        var listRequest = new HttpRequestMessage(HttpMethod.Get, "/repositories");
        listRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);

        var repos = await listResponse.Content.ReadFromJsonAsync<List<RepoDto>>();
        var repo = Assert.Single(repos!, r => r.FullName == "acme/svc");
        Assert.True(repo.IsActive);

        var deactivateRequest = new HttpRequestMessage(HttpMethod.Post, $"/repositories/{repo.Id}/deactivate");
        deactivateRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var deactivateResponse = await client.SendAsync(deactivateRequest);
        Assert.Equal(HttpStatusCode.OK, deactivateResponse.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevIaDbContext>();
        var stored = await db.Repositories.AsNoTracking().FirstAsync(r => r.GithubRepoId == 555);
        Assert.False(stored.IsActive);

        // The installation id from the webhook is captured (used later to mint GitHub App tokens).
        var org = await db.Organizations.AsNoTracking().FirstAsync(o => o.GithubOrgId == 100);
        Assert.Equal(999, org.InstallationId);
    }

    private WebApplicationFactory<Program> CreateFactory() =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:Postgres", _postgres.GetConnectionString());
            builder.UseSetting("RabbitMq:Host", _rabbitMq.Hostname);
            builder.UseSetting("RabbitMq:Port", _rabbitMq.GetMappedPublicPort(5672).ToString());
            builder.UseSetting("RabbitMq:Username", "devia");
            builder.UseSetting("RabbitMq:Password", "devia");
            builder.UseSetting("RabbitMq:QueueName", "review-jobs-test");
            builder.UseSetting("GitHub:WebhookSecret", WebhookSecret);
            builder.UseSetting("Auth:SigningKey", "test-signing-key-at-least-32-bytes-long-1234567890");
            builder.UseSetting("Auth:Issuer", "DevIa");
            builder.UseSetting("Auth:Audience", "DevIa");
            builder.UseSetting("Auth:ReviewerLogins:0", "reviewer1");

            // Run the Worker's consumer inside the same test host, with the LLM/GitHub
            // boundaries faked (the real pipeline is unit-tested separately).
            builder.ConfigureServices(services =>
            {
                services.AddScoped<IDiffSource, FakeDiffSource>();
                services.AddScoped<IReviewPipeline, FakeReviewPipeline>();

                // Real MongoDB store against the test container (the heavy result payload).
                services.Configure<MongoOptions>(o =>
                {
                    o.ConnectionString = _mongo.GetConnectionString();
                    o.Database = MongoDatabase;
                });
                services.AddSingleton<IMongoClient>(_ => new MongoClient(_mongo.GetConnectionString()));
                services.AddSingleton(sp => sp.GetRequiredService<IMongoClient>().GetDatabase(MongoDatabase));
                services.AddScoped<IReviewResultStore, MongoReviewResultStore>();

                services.AddScoped<IGitHubOAuthClient, FakeGitHubOAuthClient>();
                // No GitHub App credentials in tests: fake the verdict reflection (no network).
                services.AddScoped<IVerdictNotifier, RecordingVerdictNotifier>();
                services.AddScoped<ProcessReviewJob>();
                services.AddHostedService<ReviewJobConsumer>();
                services.AddSingleton<ILoggerProvider>(new CapturingLoggerProvider(_logs));
            });
        });

    private async Task<HttpResponseMessage> PostWebhookAsync(HttpClient client, string headSha)
    {
        var body = Encoding.UTF8.GetBytes(Payload(headSha));
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("X-GitHub-Event", "pull_request");
        request.Headers.Add("X-Hub-Signature-256", Sign(body, WebhookSecret));
        return await client.SendAsync(request);
    }

    private static async Task<Guid> GetReviewIdAsync(WebApplicationFactory<Program> factory, string headSha)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevIaDbContext>();
        var review = await db.Reviews.AsNoTracking().FirstAsync(r => r.HeadSha == headSha);
        return review.Id;
    }

    private static async Task<string> GetTokenAsync(HttpClient client, string code)
    {
        var response = await client.GetAsync($"/auth/github/callback?code={code}");
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<TokenResponse>();
        return payload!.Token;
    }

    private static async Task<HttpResponseMessage> PostVerdictAsync(HttpClient client, Guid reviewId, string token, string decision)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, $"/reviews/{reviewId}/verdict")
        {
            Content = JsonContent.Create(new { decision, justification = (string?)null })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await client.SendAsync(request);
    }

    private sealed class FakeGitHubOAuthClient : IGitHubOAuthClient
    {
        public Task<GitHubUserProfile> ExchangeCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(code == "reviewer-code"
                ? new GitHubUserProfile(900, "reviewer1", "Reviewer One", "rev@example.com", null)
                : new GitHubUserProfile(901, "developer2", "Dev Two", "dev@example.com", null));
    }

    private async Task<HttpResponseMessage> PostInstallationAsync(HttpClient client, string json)
    {
        var body = Encoding.UTF8.GetBytes(json);
        var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = new ByteArrayContent(body)
        };
        request.Content.Headers.ContentType = new("application/json");
        request.Headers.Add("X-GitHub-Event", "installation");
        request.Headers.Add("X-Hub-Signature-256", Sign(body, WebhookSecret));
        return await client.SendAsync(request);
    }

    private sealed record TokenResponse(string Token, Guid UserId, string Login);

    private sealed record RepoDto(Guid Id, string FullName, bool IsActive);

    private sealed record ReviewListDto(Guid Id, string RepositoryFullName, string Status);

    private sealed record ReviewDetailDto(Guid Id, string? Summary, List<ReviewFindingReadDto> Findings);

    private sealed record ReviewFindingReadDto(string Severity, string Title);

    private sealed class CapturingLoggerProvider(ConcurrentQueue<string> sink) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new CapturingLogger(categoryName, sink);
        public void Dispose() { }

        private sealed class CapturingLogger(string category, ConcurrentQueue<string> sink) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Warning;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
                Func<TState, Exception?, string> formatter)
            {
                if (!IsEnabled(logLevel)) return;
                var line = $"[{logLevel}] {category}: {formatter(state, exception)}";
                if (exception is not null) line += "\n" + exception.GetType().Name + ": " + exception.Message;
                sink.Enqueue(line);
            }
        }
    }

    private sealed class RecordingVerdictNotifier : IVerdictNotifier
    {
        public Task PublishAsync(VerdictNotification notification, CancellationToken ct = default)
            => Task.CompletedTask;
    }

    private sealed class FakeDiffSource : IDiffSource
    {
        public Task<string> GetDiffAsync(string repoFullName, int prNumber, string headSha, CancellationToken ct = default)
            => Task.FromResult("diff --git a/file b/file");
    }

    private sealed class FakeReviewPipeline : IReviewPipeline
    {
        public Task<ReviewAssessment> RunAsync(ReviewPipelineInput input, CancellationToken ct = default)
            => Task.FromResult(new ReviewAssessment(
                Summary: "Looks fine.",
                RiskScore: 10,
                ModelProvider: "Fake",
                ModelVersion: "test",
                TokensUsed: 0,
                Findings: [new FindingDraft(Severity.Info, FindingCategory.Style, "file", 1, "Nit", "Minor nit", null)],
                Prompts: [new PromptTrace("analyze", "test", "review this diff")],
                RawResponse: """{"summary":"Looks fine.","findings":[]}"""));
    }

    private static async Task MigrateAsync(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DevIaDbContext>();
        await db.Database.MigrateAsync();
    }

    private static async Task<ReviewStatus?> WaitForStatusAsync(
        WebApplicationFactory<Program> factory, string headSha, ReviewStatus expected, TimeSpan timeout)
    {
        ReviewStatus? last = null;
        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<DevIaDbContext>();
            var review = await db.Reviews.AsNoTracking().FirstOrDefaultAsync(r => r.HeadSha == headSha);
            if (review is not null)
            {
                last = review.Status;
                if (review.Status == expected)
                    return review.Status;
            }
            await Task.Delay(300);
        }
        return last; // surface the last observed status for a clearer failure message
    }

    private static string Sign(byte[] body, string secret)
        => "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();

    private static string Payload(string headSha) =>
        """{"action":"opened","repository":{"id":200,"full_name":"acme/app","default_branch":"main","owner":{"id":100,"login":"acme"}},"pull_request":{"number":7,"title":"Add feature","state":"open","html_url":"https://github.com/acme/app/pull/7","head":{"sha":"__SHA__","ref":"feature"},"base":{"sha":"def456","ref":"main"},"user":{"id":300,"login":"dev"}}}"""
            .Replace("__SHA__", headSha);
}
