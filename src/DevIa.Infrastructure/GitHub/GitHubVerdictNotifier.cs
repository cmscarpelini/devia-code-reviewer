using System.Net.Http.Headers;
using System.Net.Http.Json;
using DevIa.Application.Reviews;
using DevIa.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace DevIa.Infrastructure.GitHub;

/// <summary>
/// Reflects a human verdict on the GitHub PR (SPEC-0003): a completed Check Run (✅ success /
/// ❌ failure) plus an Issue Comment that notifies the author. Uses an installation access token;
/// if the repo is not connected to the App, it logs and skips (best-effort — the verdict is the
/// source of truth and is already persisted).
/// </summary>
public sealed class GitHubVerdictNotifier(
    HttpClient httpClient,
    GitHubInstallationAuthenticator authenticator,
    ILogger<GitHubVerdictNotifier> logger) : IVerdictNotifier
{
    private const string CheckRunName = "DevIA Code Review";

    public async Task PublishAsync(VerdictNotification notification, CancellationToken cancellationToken = default)
    {
        var token = await authenticator.GetTokenForRepoAsync(notification.RepoFullName, cancellationToken);
        if (token is null)
        {
            logger.LogWarning(
                "No installation token for {Repo}; skipping GitHub reflection of verdict {Decision}.",
                notification.RepoFullName, notification.Decision);
            return;
        }

        await PostCheckRunAsync(notification, token, cancellationToken);
        await PostCommentAsync(notification, token, cancellationToken);
    }

    private async Task PostCheckRunAsync(VerdictNotification n, string token, CancellationToken cancellationToken)
    {
        var approved = n.Decision == VerdictDecision.Approved;
        var body = new
        {
            name = CheckRunName,
            head_sha = n.HeadSha,
            status = "completed",
            conclusion = approved ? "success" : "failure",
            output = new
            {
                title = approved ? "Approved by reviewer" : "Changes requested",
                summary = n.Justification ?? (approved ? "Approved." : "Rejected.")
            }
        };

        await SendAsync(HttpMethod.Post, $"/repos/{n.RepoFullName}/check-runs", body, token, cancellationToken);
    }

    private async Task PostCommentAsync(VerdictNotification n, string token, CancellationToken cancellationToken)
    {
        var verdict = n.Decision == VerdictDecision.Approved ? "✅ **Approved**" : "❌ **Rejected**";
        var justification = string.IsNullOrWhiteSpace(n.Justification) ? "" : $"\n\n{n.Justification}";
        var body = new { body = $"{verdict} by the DevIA reviewer.{justification}" };

        await SendAsync(
            HttpMethod.Post, $"/repos/{n.RepoFullName}/issues/{n.PrNumber}/comments", body, token, cancellationToken);
    }

    private async Task SendAsync(
        HttpMethod method, string path, object body, string token, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, $"https://api.github.com{path}")
        {
            Content = JsonContent.Create(body)
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
