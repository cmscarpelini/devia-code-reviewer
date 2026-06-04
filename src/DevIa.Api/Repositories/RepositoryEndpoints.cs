using DevIa.Application.Abstractions.Persistence;
using DevIa.Application.Identity;

namespace DevIa.Api.Repositories;

public static class RepositoryEndpoints
{
    public static IEndpointRouteBuilder MapRepositoryEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/repositories").RequireAuthorization();

        // Minimal read-only list of connected repositories (SPEC-0002 management screen).
        group.MapGet("/", async (ICodeRepositoryRepository repositories, CancellationToken cancellationToken) =>
        {
            var items = await repositories.ListAllAsync(cancellationToken);
            return Results.Ok(items.Select(r => new
            {
                id = r.Id,
                fullName = r.FullName,
                isActive = r.IsActive,
                organizationId = r.OrganizationId
            }));
        });

        group.MapPost("/{repositoryId:guid}/activate",
            (Guid repositoryId, SetRepositoryActive handler, CancellationToken ct) =>
                SetActiveAsync(repositoryId, true, handler, ct));

        group.MapPost("/{repositoryId:guid}/deactivate",
            (Guid repositoryId, SetRepositoryActive handler, CancellationToken ct) =>
                SetActiveAsync(repositoryId, false, handler, ct));

        return app;
    }

    private static async Task<IResult> SetActiveAsync(
        Guid repositoryId, bool active, SetRepositoryActive handler, CancellationToken cancellationToken)
    {
        var found = await handler.HandleAsync(repositoryId, active, cancellationToken);
        return found
            ? Results.Ok(new { repositoryId, isActive = active })
            : Results.NotFound(new { error = $"Repository {repositoryId} not found." });
    }
}
