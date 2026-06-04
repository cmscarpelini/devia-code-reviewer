using DevIa.Application.Identity;
using DevIa.Infrastructure.Auth;
using Microsoft.Extensions.Options;

namespace DevIa.Api.Auth;

public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/auth/github/login", (IOptions<AuthOptions> options) =>
        {
            var clientId = options.Value.GitHubClientId;
            var url = $"https://github.com/login/oauth/authorize?client_id={Uri.EscapeDataString(clientId)}&scope=read:user%20user:email";
            return Results.Redirect(url);
        });

        app.MapGet("/auth/github/callback", async (
            string? code, AuthenticateWithGitHub handler, IOptions<AuthOptions> options, CancellationToken cancellationToken) =>
        {
            if (string.IsNullOrWhiteSpace(code))
                return Results.BadRequest(new { error = "code is required" });

            var result = await handler.HandleAsync(code, cancellationToken);

            // SPA flow: redirect to the frontend with the token in the fragment, if configured.
            var frontend = options.Value.FrontendLoginUrl;
            if (!string.IsNullOrWhiteSpace(frontend))
                return Results.Redirect($"{frontend}#token={Uri.EscapeDataString(result.Token)}");

            return Results.Ok(new { token = result.Token, userId = result.UserId, login = result.Login });
        });

        return app;
    }
}
