using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using DevIa.Application.Identity;
using DevIa.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace DevIa.Infrastructure.GitHub;

/// <summary>Real GitHub OAuth code exchange (code → access token → user profile).</summary>
public sealed class GitHubOAuthClient(HttpClient httpClient, IOptions<AuthOptions> options) : IGitHubOAuthClient
{
    private readonly AuthOptions _options = options.Value;

    public async Task<GitHubUserProfile> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var tokenRequest = new HttpRequestMessage(HttpMethod.Post, "https://github.com/login/oauth/access_token")
        {
            Content = JsonContent.Create(new
            {
                client_id = _options.GitHubClientId,
                client_secret = _options.GitHubClientSecret,
                code
            })
        };
        tokenRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var tokenResponse = await httpClient.SendAsync(tokenRequest, cancellationToken);
        tokenResponse.EnsureSuccessStatusCode();
        var token = await tokenResponse.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GitHub did not return an access token.");

        using var userRequest = new HttpRequestMessage(HttpMethod.Get, "https://api.github.com/user");
        userRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.AccessToken);

        using var userResponse = await httpClient.SendAsync(userRequest, cancellationToken);
        userResponse.EnsureSuccessStatusCode();
        var profile = await userResponse.Content.ReadFromJsonAsync<GitHubUser>(cancellationToken)
            ?? throw new InvalidOperationException("GitHub did not return a user profile.");

        return new GitHubUserProfile(profile.Id, profile.Login, profile.Name, profile.Email, profile.AvatarUrl);
    }

    private sealed record AccessTokenResponse([property: JsonPropertyName("access_token")] string AccessToken);

    private sealed record GitHubUser(
        [property: JsonPropertyName("id")] long Id,
        [property: JsonPropertyName("login")] string Login,
        [property: JsonPropertyName("name")] string? Name,
        [property: JsonPropertyName("email")] string? Email,
        [property: JsonPropertyName("avatar_url")] string? AvatarUrl);
}
