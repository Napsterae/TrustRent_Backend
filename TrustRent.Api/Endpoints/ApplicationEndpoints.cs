using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class ApplicationEndpoints
{
    public static void MapApplicationEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api");

        // Tenant submits an application
        group.MapPost("/properties/{propertyId:guid}/applications", async (Guid propertyId, [FromBody] SubmitApplicationDto dto, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            var application = await service.SubmitApplicationAsync(propertyId, tenantId, dto);
            return Results.Ok(application);
        }).RequireAuthorization();

        // Landlord gets applications for a property
        group.MapGet("/properties/{propertyId:guid}/applications", async (Guid propertyId, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid landlordId))
                return Results.Unauthorized();

            var applications = await service.GetApplicationsForPropertyAsync(propertyId, landlordId);
            return Results.Ok(applications);
        }).RequireAuthorization();

        // Tenant gets their own applications
        group.MapGet("/applications/tenant", async (IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            var applications = await service.GetApplicationsForTenantAsync(tenantId);
            return Results.Ok(applications);
        }).RequireAuthorization();

        // Specific Application Action (Landlord or Tenant update visit logic)
        group.MapGet("/applications/{id:guid}", async (Guid id, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            // Needs to be added to IApplicationService
            var application = await service.GetApplicationByIdAsync(id, userId);
            
            if (application == null) return Results.NotFound();

            return Results.Ok(application);
        }).RequireAuthorization();

        group.MapPut("/applications/{id:guid}/visit", async (Guid id, [FromBody] UpdateApplicationVisitDto dto, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            var result = await service.UpdateVisitStatusAsync(id, userId, dto);
            return Results.Ok(result);
        }).RequireAuthorization();
    }
}
