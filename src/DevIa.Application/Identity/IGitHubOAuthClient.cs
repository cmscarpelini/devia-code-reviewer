namespace DevIa.Application.Identity;

/// <summary>The GitHub user profile obtained after exchanging an OAuth code.</summary>
public sealed record GitHubUserProfile(
    long GithubUserId,
    string Login,
    string? Name,
    string? Email,
    string? AvatarUrl);

/// <summary>Exchanges an OAuth callback code for the authenticated GitHub user's profile.</summary>
public interface IGitHubOAuthClient
{
    Task<GitHubUserProfile> ExchangeCodeAsync(string code, CancellationToken cancellationToken = default);
}
