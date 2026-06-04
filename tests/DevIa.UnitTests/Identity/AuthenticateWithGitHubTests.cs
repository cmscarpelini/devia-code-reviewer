using DevIa.Application.Identity;
using DevIa.Domain.Identity;
using DevIa.UnitTests.PullRequests; // reuse FakeUserRepository + FakeUnitOfWork
using Microsoft.Extensions.Logging.Abstractions;

namespace DevIa.UnitTests.Identity;

public class AuthenticateWithGitHubTests
{
    private sealed class FakeGitHubOAuthClient(GitHubUserProfile profile) : IGitHubOAuthClient
    {
        public Task<GitHubUserProfile> ExchangeCodeAsync(string code, CancellationToken ct = default)
            => Task.FromResult(profile);
    }

    private sealed class FakeUserTokenService : IUserTokenService
    {
        public User? LastUser;
        public string CreateToken(User user)
        {
            LastUser = user;
            return "token-for-" + user.Login;
        }
    }

    [Fact]
    public async Task First_login_creates_the_user_and_returns_a_token()
    {
        var users = new FakeUserRepository();
        var tokens = new FakeUserTokenService();
        var handler = new AuthenticateWithGitHub(
            new FakeGitHubOAuthClient(new GitHubUserProfile(42, "octocat", "Octo", "o@example.com", null)),
            users, tokens, new FakeUnitOfWork(), NullLogger<AuthenticateWithGitHub>.Instance);

        var result = await handler.HandleAsync("code");

        Assert.Equal("token-for-octocat", result.Token);
        Assert.Equal("octocat", result.Login);
        var user = Assert.Single(users.Items);
        Assert.Equal(42, user.GithubUserId);
    }

    [Fact]
    public async Task Returning_user_has_profile_updated()
    {
        var users = new FakeUserRepository();
        users.Add(new User(42, "old-login"));
        var handler = new AuthenticateWithGitHub(
            new FakeGitHubOAuthClient(new GitHubUserProfile(42, "new-login", "New", null, null)),
            users, new FakeUserTokenService(), new FakeUnitOfWork(), NullLogger<AuthenticateWithGitHub>.Instance);

        await handler.HandleAsync("code");

        var user = Assert.Single(users.Items);
        Assert.Equal("new-login", user.Login);
    }
}
