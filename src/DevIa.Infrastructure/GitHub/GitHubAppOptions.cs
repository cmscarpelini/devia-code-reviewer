namespace DevIa.Infrastructure.GitHub;

/// <summary>
/// GitHub App settings (config section "GitHub"). <see cref="AppId"/> and <see cref="PrivateKey"/>
/// authenticate the App to mint installation access tokens; they are credentials and live in
/// user-secrets (dev) / Key Vault (prod). When unset, authenticated calls degrade gracefully
/// (diff falls back to unauthenticated public access; the verdict notifier logs and skips).
/// </summary>
public sealed class GitHubAppOptions
{
    /// <summary>HMAC secret for verifying inbound webhook signatures (already used by the API).</summary>
    public string WebhookSecret { get; set; } = "";

    /// <summary>The numeric GitHub App id, used as the <c>iss</c> claim of the App JWT.</summary>
    public string AppId { get; set; } = "";

    /// <summary>The App's RSA private key in PEM format (PKCS#1 or PKCS#8).</summary>
    public string PrivateKey { get; set; } = "";

    public string ApiBaseUrl { get; set; } = "https://api.github.com";

    public string UserAgent { get; set; } = "DevIa-CodeReviewer";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(AppId) && !string.IsNullOrWhiteSpace(PrivateKey);
}
