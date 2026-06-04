using System.Security.Cryptography;
using System.Text;

namespace DevIa.Infrastructure.GitHub;

/// <summary>
/// Validates the GitHub webhook signature header <c>X-Hub-Signature-256</c>
/// (HMAC-SHA256 over the raw request body), using a constant-time comparison.
/// </summary>
public static class GitHubSignature
{
    private const string Prefix = "sha256=";

    public static bool IsValid(byte[] body, string? signatureHeader, string secret)
    {
        if (string.IsNullOrEmpty(secret)) return false;
        if (string.IsNullOrEmpty(signatureHeader) || !signatureHeader.StartsWith(Prefix, StringComparison.Ordinal))
            return false;

        var provided = signatureHeader[Prefix.Length..];
        var hash = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body);
        var expected = Convert.ToHexString(hash).ToLowerInvariant();

        // FixedTimeEquals returns false for differing lengths and avoids timing leaks.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(expected),
            Encoding.ASCII.GetBytes(provided));
    }
}
