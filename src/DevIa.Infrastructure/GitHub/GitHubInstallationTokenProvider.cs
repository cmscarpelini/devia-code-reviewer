using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace DevIa.Infrastructure.GitHub;

/// <summary>Mints (and caches) GitHub App installation access tokens.</summary>
public interface IGitHubInstallationTokenProvider
{
    /// <summary>
    /// Returns a valid installation access token for <paramref name="installationId"/>, reusing a
    /// cached one until it nears expiry. Throws if the App is not configured.
    /// </summary>
    Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default);
}

/// <summary>
/// GitHub App authentication: signs a short-lived App JWT (RS256, iss = App id) with the App's
/// private key, exchanges it for an installation access token, and caches that token per
/// installation until shortly before it expires. Thread-safe; registered as a singleton.
/// </summary>
public sealed class GitHubInstallationTokenProvider : IGitHubInstallationTokenProvider
{
    // Refresh a little before the real expiry to absorb clock skew and in-flight requests.
    private static readonly TimeSpan RenewBefore = TimeSpan.FromMinutes(1);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly GitHubAppOptions _options;
    private readonly ILogger<GitHubInstallationTokenProvider> _logger;
    private readonly RsaSecurityKey? _signingKey;
    private readonly ConcurrentDictionary<long, CachedToken> _cache = new();

    public GitHubInstallationTokenProvider(
        IHttpClientFactory httpClientFactory,
        IOptions<GitHubAppOptions> options,
        ILogger<GitHubInstallationTokenProvider> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;

        if (_options.IsConfigured)
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(_options.PrivateKey);
            _signingKey = new RsaSecurityKey(rsa);
        }
    }

    public async Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default)
    {
        if (_signingKey is null)
            throw new InvalidOperationException("GitHub App is not configured (missing AppId/PrivateKey).");

        if (_cache.TryGetValue(installationId, out var cached) && cached.ExpiresAt - RenewBefore > DateTimeOffset.UtcNow)
            return cached.Token;

        var token = await FetchInstallationTokenAsync(installationId, cancellationToken);
        _cache[installationId] = token;
        return token.Token;
    }

    private async Task<CachedToken> FetchInstallationTokenAsync(long installationId, CancellationToken cancellationToken)
    {
        var appJwt = CreateAppJwt();

        var client = _httpClientFactory.CreateClient("github");
        using var request = new HttpRequestMessage(
            HttpMethod.Post, $"{_options.ApiBaseUrl}/app/installations/{installationId}/access_tokens");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appJwt);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        request.Headers.UserAgent.ParseAdd(_options.UserAgent);

        using var response = await client.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken)
            ?? throw new InvalidOperationException("GitHub did not return an installation token.");

        _logger.LogDebug("Minted installation token for installation {InstallationId} (expires {ExpiresAt}).",
            installationId, payload.ExpiresAt);

        return new CachedToken(payload.Token, payload.ExpiresAt);
    }

    private string CreateAppJwt()
    {
        var now = DateTime.UtcNow;
        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.AppId,
            IssuedAt = now.AddSeconds(-30), // tolerate minor clock drift against GitHub
            NotBefore = now.AddSeconds(-30),
            Expires = now.AddMinutes(9),    // GitHub caps App JWTs at 10 minutes
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private sealed record CachedToken(string Token, DateTimeOffset ExpiresAt);

    private sealed record AccessTokenResponse(
        [property: JsonPropertyName("token")] string Token,
        [property: JsonPropertyName("expires_at")] DateTimeOffset ExpiresAt);
}
