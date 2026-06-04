using DevIa.Application.Reviews;
using DevIa.Domain.Enums;

namespace DevIa.Api.Reviews;

public static class ReviewEndpoints
{
    public static IEndpointRouteBuilder MapReviewEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/reviews").RequireAuthorization();

        // Review queue (SPEC-0004): defaults to AwaitingHumanReview, paginated.
        group.MapGet("/", async (
            string? status, int? page, int? pageSize, IReviewQueries queries, CancellationToken cancellationToken) =>
        {
            var reviewStatus = Enum.TryParse<ReviewStatus>(status, ignoreCase: true, out var parsed)
                ? parsed
                : ReviewStatus.AwaitingHumanReview;

            var items = await queries.ListByStatusAsync(
                reviewStatus, Math.Max(page ?? 1, 1), Math.Clamp(pageSize ?? 20, 1, 100), cancellationToken);

            return Results.Ok(items);
        });

        // Review detail: summary + findings grouped by severity.
        group.MapGet("/{reviewId:guid}", async (Guid reviewId, IReviewQueries queries, CancellationToken cancellationToken) =>
        {
            var detail = await queries.GetDetailAsync(reviewId, cancellationToken);
            return detail is null ? Results.NotFound() : Results.Ok(detail);
        });

        return app;
    }
}
