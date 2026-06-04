using System.Security.Claims;
using DevIa.Application.Reviews;
using DevIa.Domain.Common;
using DevIa.Domain.Enums;
using Microsoft.IdentityModel.JsonWebTokens;

namespace DevIa.Api.Reviews;

public static class VerdictEndpoints
{
    public static IEndpointRouteBuilder MapVerdictEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/reviews/{reviewId:guid}/verdict", HandleAsync)
            .RequireAuthorization("CanReview");
        return app;
    }

    private static async Task<IResult> HandleAsync(
        Guid reviewId,
        RecordVerdictRequest request,
        RecordVerdict handler,
        ClaimsPrincipal user,
        CancellationToken cancellationToken)
    {
        // The reviewer is the authenticated user (the CanReview policy guarantees the role).
        var subject = user.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (!Guid.TryParse(subject, out var reviewerUserId))
            return Results.Unauthorized();

        if (!Enum.TryParse<VerdictDecision>(request.Decision, ignoreCase: true, out var decision))
            return Results.BadRequest(new { error = $"Invalid decision '{request.Decision}'. Expected 'Approved' or 'Rejected'." });

        try
        {
            var result = await handler.HandleAsync(reviewId, reviewerUserId, decision, request.Justification, cancellationToken);

            return result.Status == RecordVerdictStatus.Recorded
                ? Results.Ok(new { verdictId = result.VerdictId, decision = result.Decision?.ToString() })
                : Results.NotFound(new { error = $"Review {reviewId} not found." });
        }
        catch (DomainException ex)
        {
            // Rule violation (wrong status, already decided, missing justification).
            return Results.Conflict(new { error = ex.Message });
        }
    }
}

internal sealed record RecordVerdictRequest(string Decision, string? Justification);
