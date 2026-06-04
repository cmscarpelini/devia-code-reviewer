using DevIa.Application.Abstractions.Persistence;
using DevIa.Domain.Identity;
using Microsoft.Extensions.Logging;

namespace DevIa.Application.Identity;

public sealed record AuthResult(string Token, Guid UserId, string Login);

/// <summary>
/// Authenticates a user via GitHub OAuth (SPEC-0002): exchanges the code for the profile,
/// upserts the <see cref="User"/>, and issues a signed access token.
/// </summary>
public sealed class AuthenticateWithGitHub(
    IGitHubOAuthClient gitHub,
    IUserRepository users,
    IUserTokenService tokens,
    IUnitOfWork unitOfWork,
    ILogger<AuthenticateWithGitHub> logger)
{
    public async Task<AuthResult> HandleAsync(string code, CancellationToken cancellationToken = default)
    {
        var profile = await gitHub.ExchangeCodeAsync(code, cancellationToken);

        var user = await users.GetByGithubIdAsync(profile.GithubUserId, cancellationToken);
        if (user is null)
        {
            user = new User(profile.GithubUserId, profile.Login, profile.Name, profile.Email, profile.AvatarUrl);
            users.Add(user);
        }
        else
        {
            user.UpdateProfile(profile.Login, profile.Name, profile.Email, profile.AvatarUrl);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var token = tokens.CreateToken(user);
        logger.LogInformation("User {Login} ({UserId}) authenticated via GitHub.", user.Login, user.Id);
        return new AuthResult(token, user.Id, user.Login);
    }
}
