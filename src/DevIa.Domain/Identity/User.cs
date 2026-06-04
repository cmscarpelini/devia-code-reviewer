using DevIa.Domain.Common;

namespace DevIa.Domain.Identity;

/// <summary>A GitHub user (PR author or reviewer).</summary>
public sealed class User : Entity
{
    public long GithubUserId { get; private set; }
    public string Login { get; private set; } = null!;
    public string? Name { get; private set; }
    public string? Email { get; private set; }
    public string? AvatarUrl { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private User() { } // EF

    public User(long githubUserId, string login, string? name = null, string? email = null, string? avatarUrl = null)
    {
        if (githubUserId <= 0) throw new DomainException("GithubUserId must be positive.");
        if (string.IsNullOrWhiteSpace(login)) throw new DomainException("Login is required.");

        Id = Guid.NewGuid();
        GithubUserId = githubUserId;
        Login = login;
        Name = name;
        Email = email;
        AvatarUrl = avatarUrl;
        CreatedAt = DateTimeOffset.UtcNow;
    }

    /// <summary>Refreshes the profile from GitHub on subsequent sign-ins (SPEC-0002).</summary>
    public void UpdateProfile(string login, string? name, string? email, string? avatarUrl)
    {
        if (string.IsNullOrWhiteSpace(login)) throw new DomainException("Login is required.");
        Login = login;
        Name = name;
        Email = email;
        AvatarUrl = avatarUrl;
    }
}
