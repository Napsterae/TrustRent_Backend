using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class ReviewEndpoints
{
    public static void MapReviewEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/reviews");

        // POST /api/reviews — submit a review
        group.MapPost("/", async ([FromBody] CreateReviewRequest dto,
            IReviewService service, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                var review = await service.SubmitReviewAsync(userId, dto);
                return Results.Ok(review);
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
        }).RequireAuthorization();

        // GET /api/reviews/user/{userId} — public reviews for a user
        group.MapGet("/user/{userId:guid}", async (Guid userId, IReviewService service) =>
        {
            var reviews = await service.GetPublicReviewsForUserAsync(userId);
            return Results.Ok(reviews);
        });

        // GET /api/reviews/user/{userId}/summary — review summary for a user
        group.MapGet("/user/{userId:guid}/summary", async (Guid userId, IReviewService service) =>
        {
            var summary = await service.GetReviewSummaryForUserAsync(userId);
            return Results.Ok(summary);
        });

        // GET /api/reviews/pending — pending reviews for authenticated user
        group.MapGet("/pending", async (IReviewService service, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var pending = await service.GetPendingReviewsForUserAsync(userId);
            return Results.Ok(pending);
        }).RequireAuthorization();

        // GET /api/reviews/lease/{leaseId} — published reviews for a lease
        group.MapGet("/lease/{leaseId:guid}", async (Guid leaseId,
            IReviewService service, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var reviews = await service.GetReviewsByLeaseAsync(leaseId, userId);
            return Results.Ok(reviews);
        }).RequireAuthorization();

        // GET /api/reviews/ticket/{ticketId} — published reviews for a ticket
        group.MapGet("/ticket/{ticketId:guid}", async (Guid ticketId,
            IReviewService service, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            var reviews = await service.GetReviewsByTicketAsync(ticketId, userId);
            return Results.Ok(reviews);
        }).RequireAuthorization();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        userId = Guid.Empty;
        var claim = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return claim != null && Guid.TryParse(claim, out userId);
    }
}
