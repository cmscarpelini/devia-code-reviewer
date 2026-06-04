namespace DevIa.Infrastructure.Auth;

/// <summary>Authentication settings (bound from the "Auth" config section).</summary>
public sealed class AuthOptions
{
    public string SigningKey { get; set; } = "";
    public string Issuer { get; set; } = "DevIa";
    public string Audience { get; set; } = "DevIa";
    public int AccessTokenMinutes { get; set; } = 60;

    public string GitHubClientId { get; set; } = "";
    public string GitHubClientSecret { get; set; } = "";

    /// <summary>GitHub logins that receive the Reviewer role (MVP RBAC; full model in Phase 2).</summary>
    public string[] ReviewerLogins { get; set; } = [];

    /// <summary>
    /// If set, the OAuth callback redirects here with the token in the fragment
    /// (<c>{FrontendLoginUrl}#token=...</c>) instead of returning JSON. Enables the SPA flow.
    /// </summary>
    public string? FrontendLoginUrl { get; set; }
}
