using System.Security.Cryptography;
using System.Text;
using DevIa.Infrastructure.GitHub;

namespace DevIa.UnitTests.Webhooks;

public class GitHubSignatureTests
{
    private const string Secret = "test-secret";
    private static readonly byte[] Body = Encoding.UTF8.GetBytes("""{"action":"opened"}""");

    private static string Sign(byte[] body, string secret)
        => "sha256=" + Convert.ToHexString(
            HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), body)).ToLowerInvariant();

    [Fact]
    public void Valid_signature_passes()
        => Assert.True(GitHubSignature.IsValid(Body, Sign(Body, Secret), Secret));

    [Fact]
    public void Tampered_body_fails()
    {
        var signature = Sign(Body, Secret);
        var tampered = Encoding.UTF8.GetBytes("""{"action":"closed"}""");

        Assert.False(GitHubSignature.IsValid(tampered, signature, Secret));
    }

    [Fact]
    public void Wrong_secret_fails()
        => Assert.False(GitHubSignature.IsValid(Body, Sign(Body, "other-secret"), Secret));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("deadbeef")]                 // missing "sha256=" prefix
    [InlineData("sha256=notvalidhex")]       // wrong length / content
    public void Malformed_or_missing_header_fails(string? header)
        => Assert.False(GitHubSignature.IsValid(Body, header, Secret));

    [Fact]
    public void Empty_secret_fails()
        => Assert.False(GitHubSignature.IsValid(Body, Sign(Body, Secret), ""));
}
