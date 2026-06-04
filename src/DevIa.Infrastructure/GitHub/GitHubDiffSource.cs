using System.Net.Http.Headers;
using DevIa.Application.Reviews;

namespace DevIa.Infrastructure.GitHub;

/// <summary>
/// Fetches a PR's unified diff from the GitHub REST API (diff media type). Uses an installation
/// access token when the repo is connected to the App (required for private repos); falls back to
/// unauthenticated access for public repositories.
/// </summary>
public sealed class GitHubDiffSource(HttpClient httpClient, GitHubInstallationAuthenticator authenticator) : IDiffSource
{
    public async Task<string> GetDiffAsync(string repoFullName, int prNumber, string headSha, CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get, $"https://api.github.com/repos/{repoFullName}/pulls/{prNumber}");
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github.v3.diff"));

        var token = await authenticator.GetTokenForRepoAsync(repoFullName, cancellationToken);
        if (token is not null)
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);

        using var response = await httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }
}
