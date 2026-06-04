using System.Text.Json;
using DevIa.Application.Identity;
using DevIa.Application.PullRequests;
using DevIa.Infrastructure.GitHub;

namespace DevIa.Api.Webhooks;

public static class GitHubWebhookEndpoints
{
    public static IEndpointRouteBuilder MapGitHubWebhook(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhooks/github", HandleAsync);
        return app;
    }

    private static async Task<IResult> HandleAsync(
        HttpRequest request,
        IConfiguration configuration,
        PullRequestWebhookHandler pullRequestHandler,
        ProcessInstallation installationHandler,
        CancellationToken cancellationToken)
    {
        // Read the raw body bytes — required to verify the HMAC signature exactly.
        byte[] bodyBytes;
        await using (var buffer = new MemoryStream())
        {
            await request.Body.CopyToAsync(buffer, cancellationToken);
            bodyBytes = buffer.ToArray();
        }

        var secret = configuration["GitHub:WebhookSecret"];
        if (string.IsNullOrEmpty(secret))
            return Results.Problem("Webhook secret is not configured.", statusCode: StatusCodes.Status500InternalServerError);

        var signature = request.Headers["X-Hub-Signature-256"].FirstOrDefault();
        if (!GitHubSignature.IsValid(bodyBytes, signature, secret))
            return Results.Unauthorized();

        var eventType = request.Headers["X-GitHub-Event"].FirstOrDefault()?.ToLowerInvariant();

        return eventType switch
        {
            "pull_request" => await HandlePullRequestAsync(bodyBytes, pullRequestHandler, cancellationToken),
            "installation" or "installation_repositories" => await HandleInstallationAsync(bodyBytes, installationHandler, cancellationToken),
            _ => Results.Accepted(value: new { ignored = true, reason = $"event '{eventType}' not handled" })
        };
    }

    private static async Task<IResult> HandlePullRequestAsync(
        byte[] bodyBytes, PullRequestWebhookHandler handler, CancellationToken cancellationToken)
    {
        if (!TryDeserialize<GitHubPullRequestEvent>(bodyBytes, out var payload))
            return Results.BadRequest(new { error = "Invalid pull_request payload." });

        var result = await handler.HandleAsync(payload.ToInput(), cancellationToken);

        return result.Status switch
        {
            WebhookProcessingStatus.Accepted =>
                Results.Accepted($"/reviews/{result.ReviewId}", new { reviewId = result.ReviewId }),
            WebhookProcessingStatus.Duplicate =>
                Results.Accepted(value: new { reviewId = result.ReviewId, duplicate = true }),
            _ => Results.Accepted(value: new { ignored = true })
        };
    }

    private static async Task<IResult> HandleInstallationAsync(
        byte[] bodyBytes, ProcessInstallation handler, CancellationToken cancellationToken)
    {
        if (!TryDeserialize<GitHubInstallationEvent>(bodyBytes, out var payload))
            return Results.BadRequest(new { error = "Invalid installation payload." });

        var affected = await handler.HandleAsync(payload.ToInput(), cancellationToken);
        return Results.Accepted(value: new { repositoriesAffected = affected });
    }

    private static bool TryDeserialize<T>(byte[] bodyBytes, out T payload) where T : class
    {
        try
        {
            var deserialized = JsonSerializer.Deserialize<T>(bodyBytes);
            payload = deserialized!;
            return deserialized is not null;
        }
        catch (JsonException)
        {
            payload = null!;
            return false;
        }
    }
}
