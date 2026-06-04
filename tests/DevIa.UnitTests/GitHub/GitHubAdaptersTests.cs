using System.Net;
using System.Security.Cryptography;
using DevIa.Domain.Enums;
using DevIa.Domain.Identity;
using DevIa.Application.Reviews;
using DevIa.Infrastructure.GitHub;
using DevIa.UnitTests.PullRequests; // reuse the repository fakes
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevIa.UnitTests.GitHub;

public class GitHubAdaptersTests
{
    private static string TestPrivateKeyPem() => RSA.Create(2048).ExportPkcs8PrivateKeyPem();

    private static GitHubAppOptions ConfiguredOptions() =>
        new() { AppId = "123456", PrivateKey = TestPrivateKeyPem() };

    /// <summary>Records each request (method, uri, auth, body) and returns a scripted response.</summary>
    private sealed class RecordingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public readonly List<Recorded> Requests = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new Recorded(
                request.Method,
                request.RequestUri!,
                request.Headers.Authorization?.Scheme,
                request.Headers.Authorization?.Parameter,
                body));
            return responder(request);
        }
    }

    private sealed record Recorded(HttpMethod Method, Uri Uri, string? AuthScheme, string? AuthParameter, string? Body);

    private sealed class StubHttpClientFactory(HttpMessageHandler handler) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => new(handler, disposeHandler: false);
    }

    private sealed class StubTokenProvider(string token) : IGitHubInstallationTokenProvider
    {
        public int Calls { get; private set; }
        public Task<string> GetInstallationTokenAsync(long installationId, CancellationToken cancellationToken = default)
        {
            Calls++;
            return Task.FromResult(token);
        }
    }

    private static HttpResponseMessage Json(string content, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = new StringContent(content) };

    private static GitHubInstallationAuthenticator Authenticator(
        IGitHubInstallationTokenProvider tokenProvider, GitHubAppOptions options, bool withInstallation = true)
    {
        var repos = new FakeCodeRepositoryRepository();
        var orgs = new FakeOrganizationRepository();

        var org = new Organization(100, "acme");
        if (withInstallation) org.SetInstallationId(999);
        orgs.Add(org);
        repos.Add(new CodeRepository(org.Id, 200, "acme/app", "main"));

        return new GitHubInstallationAuthenticator(
            repos, orgs, tokenProvider, Options.Create(options),
            NullLogger<GitHubInstallationAuthenticator>.Instance);
    }

    // ---- Token provider ----

    [Fact]
    public async Task Token_provider_mints_an_RS256_app_jwt_and_returns_the_installation_token()
    {
        var options = ConfiguredOptions();
        var handler = new RecordingHandler(_ => Json("""{"token":"ghs_abc","expires_at":"2999-01-01T00:00:00Z"}"""));
        var provider = new GitHubInstallationTokenProvider(
            new StubHttpClientFactory(handler), Options.Create(options),
            NullLogger<GitHubInstallationTokenProvider>.Instance);

        var token = await provider.GetInstallationTokenAsync(999);

        Assert.Equal("ghs_abc", token);
        var request = Assert.Single(handler.Requests);
        Assert.EndsWith("/app/installations/999/access_tokens", request.Uri.ToString());
        Assert.Equal("Bearer", request.AuthScheme);

        // The bearer credential is the App JWT: RS256, signed, iss = App id.
        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(request.AuthParameter);
        Assert.Equal("RS256", jwt.Alg);
        Assert.Equal("123456", jwt.Issuer);
    }

    [Fact]
    public async Task Token_provider_caches_the_token_until_it_nears_expiry()
    {
        var handler = new RecordingHandler(_ => Json("""{"token":"ghs_cached","expires_at":"2999-01-01T00:00:00Z"}"""));
        var provider = new GitHubInstallationTokenProvider(
            new StubHttpClientFactory(handler), Options.Create(ConfiguredOptions()),
            NullLogger<GitHubInstallationTokenProvider>.Instance);

        await provider.GetInstallationTokenAsync(999);
        await provider.GetInstallationTokenAsync(999);

        Assert.Single(handler.Requests); // second call served from cache
    }

    [Fact]
    public async Task Token_provider_throws_when_app_not_configured()
    {
        var provider = new GitHubInstallationTokenProvider(
            new StubHttpClientFactory(new RecordingHandler(_ => Json("{}"))),
            Options.Create(new GitHubAppOptions()), // no AppId/PrivateKey
            NullLogger<GitHubInstallationTokenProvider>.Instance);

        await Assert.ThrowsAsync<InvalidOperationException>(() => provider.GetInstallationTokenAsync(999));
    }

    // ---- Authenticator ----

    [Fact]
    public async Task Authenticator_returns_null_when_app_not_configured()
    {
        var auth = Authenticator(new StubTokenProvider("tok"), new GitHubAppOptions());

        Assert.Null(await auth.GetTokenForRepoAsync("acme/app"));
    }

    [Fact]
    public async Task Authenticator_returns_null_when_org_has_no_installation()
    {
        var auth = Authenticator(new StubTokenProvider("tok"), ConfiguredOptions(), withInstallation: false);

        Assert.Null(await auth.GetTokenForRepoAsync("acme/app"));
    }

    [Fact]
    public async Task Authenticator_returns_token_for_connected_repo()
    {
        var auth = Authenticator(new StubTokenProvider("tok-123"), ConfiguredOptions());

        Assert.Equal("tok-123", await auth.GetTokenForRepoAsync("acme/app"));
    }

    // ---- Diff source ----

    [Fact]
    public async Task Diff_source_attaches_the_installation_token()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("diff --git a/f b/f")
        });
        var auth = Authenticator(new StubTokenProvider("tok-xyz"), ConfiguredOptions());
        var source = new GitHubDiffSource(new HttpClient(handler), auth);

        var diff = await source.GetDiffAsync("acme/app", 7, "sha1");

        Assert.Equal("diff --git a/f b/f", diff);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("Bearer", request.AuthScheme);
        Assert.Equal("tok-xyz", request.AuthParameter);
    }

    [Fact]
    public async Task Diff_source_falls_back_to_unauthenticated_for_unknown_repo()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("public diff")
        });
        // Authenticator with the App unconfigured → no token → no auth header.
        var auth = Authenticator(new StubTokenProvider("never"), new GitHubAppOptions());
        var source = new GitHubDiffSource(new HttpClient(handler), auth);

        var diff = await source.GetDiffAsync("octocat/Hello-World", 1, "sha");

        Assert.Equal("public diff", diff);
        Assert.Null(Assert.Single(handler.Requests).AuthScheme);
    }

    // ---- Verdict notifier ----

    [Fact]
    public async Task Notifier_posts_a_check_run_and_a_comment_when_authenticated()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var auth = Authenticator(new StubTokenProvider("tok-note"), ConfiguredOptions());
        var notifier = new GitHubVerdictNotifier(new HttpClient(handler), auth, NullLogger<GitHubVerdictNotifier>.Instance);

        await notifier.PublishAsync(new VerdictNotification("acme/app", 7, "sha1", VerdictDecision.Approved, "LGTM"));

        Assert.Equal(2, handler.Requests.Count);
        var checkRun = handler.Requests[0];
        var comment = handler.Requests[1];
        Assert.EndsWith("/repos/acme/app/check-runs", checkRun.Uri.ToString());
        Assert.Contains("\"conclusion\":\"success\"", checkRun.Body);
        Assert.EndsWith("/repos/acme/app/issues/7/comments", comment.Uri.ToString());
        Assert.All(handler.Requests, r => Assert.Equal("tok-note", r.AuthParameter));
    }

    [Fact]
    public async Task Notifier_skips_when_repo_is_not_connected()
    {
        var handler = new RecordingHandler(_ => new HttpResponseMessage(HttpStatusCode.Created));
        var auth = Authenticator(new StubTokenProvider("never"), new GitHubAppOptions()); // unconfigured → null token
        var notifier = new GitHubVerdictNotifier(new HttpClient(handler), auth, NullLogger<GitHubVerdictNotifier>.Instance);

        await notifier.PublishAsync(new VerdictNotification("acme/app", 7, "sha1", VerdictDecision.Rejected, "no"));

        Assert.Empty(handler.Requests); // best-effort: nothing posted
    }
}
