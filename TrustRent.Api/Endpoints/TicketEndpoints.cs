using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class TicketEndpoints
{
    public static void MapTicketEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/tickets");

        // GET /api/properties/{propertyId}/tickets - Get all tickets for a property (landlord only)
        app.MapGet("/api/properties/{propertyId:guid}/tickets",
            async (Guid propertyId, ITicketService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var tickets = await service.GetTicketsByPropertyAsync(propertyId, userId);
                    return Results.Ok(tickets);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // POST /api/leases/{leaseId}/tickets - Create ticket
        app.MapPost("/api/leases/{leaseId:guid}/tickets",
            async (Guid leaseId, [FromBody] CreateTicketDto dto,
                   ITicketService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var ticket = await service.CreateTicketAsync(leaseId, userId, dto);
                    return Results.Created($"/api/tickets/{ticket.Id}", ticket);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/leases/{leaseId}/tickets - Get tickets by lease
        app.MapGet("/api/leases/{leaseId:guid}/tickets",
            async (Guid leaseId, ITicketService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var tickets = await service.GetTicketsByLeaseAsync(leaseId, userId);
                    return Results.Ok(tickets);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // GET /api/tickets/{ticketId} - Get ticket details
        group.MapGet("/{ticketId:guid}",
            async (Guid ticketId, ITicketService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var ticket = await service.GetTicketByIdAsync(ticketId, userId);
                    return ticket is null ? Results.NotFound() : Results.Ok(ticket);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // PUT /api/tickets/{ticketId}/status - Update ticket status
        group.MapPut("/{ticketId:guid}/status",
            async (Guid ticketId, [FromBody] UpdateTicketStatusDto dto,
                   ITicketService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var ticket = await service.UpdateTicketStatusAsync(ticketId, userId, dto);
                    return Results.Ok(ticket);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/tickets/{ticketId}/comments - Add comment
        group.MapPost("/{ticketId:guid}/comments",
            async (Guid ticketId, [FromBody] AddTicketCommentDto dto,
                   ITicketService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var ticket = await service.AddCommentAsync(ticketId, userId, dto);
                    return Results.Ok(ticket);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/tickets/{ticketId}/attachments - Upload attachment
        group.MapPost("/{ticketId:guid}/attachments",
            async (Guid ticketId, IFormFile file,
                   ITicketService service, IImageService imageService, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

                if (file == null || file.Length == 0)
                    return Results.BadRequest("Arquivo é obrigatório.");

                if (file.Length > 10 * 1024 * 1024) // 10MB
                    return Results.BadRequest("Arquivo não pode ser maior que 10MB.");

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".webp" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();

                if (!allowedExtensions.Contains(fileExtension))
                    return Results.BadRequest("Tipo de arquivo não permitido. Use: JPG, PNG, GIF, WebP");

                try
                {
                    using var stream = file.OpenReadStream();
                    var storageUrl = await imageService.UploadImageAsync(stream, file.FileName, "tickets");

                    var ticket = await service.AddAttachmentAsync(ticketId, userId, storageUrl, file.FileName);
                    return Results.Ok(ticket);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (Exception e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization()
             .DisableAntiforgery();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var userIdClaim = user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out userId))
        {
            userId = Guid.Empty;
            return false;
        }
        return true;
    }
}
