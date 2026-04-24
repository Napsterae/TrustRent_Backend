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

            try
            {
                var applications = await service.GetApplicationsForPropertyAsync(propertyId, landlordId);
                return Results.Ok(applications);
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
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

            try
            {
                var application = await service.GetApplicationByIdAsync(id, userId);
                if (application == null) return Results.NotFound();
                return Results.Ok(application);
            }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        }).RequireAuthorization();

        group.MapPut("/applications/{id:guid}/visit", async (Guid id, [FromBody] UpdateApplicationVisitDto dto, IApplicationService service, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid userId))
                return Results.Unauthorized();

            var result = await service.UpdateVisitStatusAsync(id, userId, dto);
            return Results.Ok(result);
        }).RequireAuthorization();

        // ===== Income Validation (recibos de vencimento via IA) =====

        // Senhorio pede ao inquilino para validar rendimentos
        group.MapPost("/applications/{id:guid}/income-validation/request",
            async (Guid id, IIncomeValidationService svc, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid landlordId))
                return Results.Unauthorized();

            try
            {
                await svc.RequestValidationAsync(id, landlordId);
                return Results.NoContent();
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { Error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { Error = ex.Message }); }
        }).RequireAuthorization();

        // Inquilino faz upload dos 3 recibos
        group.MapPost("/applications/{id:guid}/income-validation",
            async (Guid id, IFormFileCollection payslips, IIncomeValidationService svc, ClaimsPrincipal user) =>
        {
            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            if (payslips == null || payslips.Count != svc.RequiredPayslipCount)
                return Results.BadRequest(new { Error = $"Tens de enviar exatamente {svc.RequiredPayslipCount} recibos de vencimento." });

            const long maxBytes = 8L * 1024 * 1024; // 8 MB por ficheiro
            var allowedMime = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "application/pdf", "image/jpeg", "image/png", "image/webp"
            };

            foreach (var f in payslips)
            {
                if (f.Length <= 0 || f.Length > maxBytes)
                    return Results.BadRequest(new { Error = $"Cada ficheiro tem de ter entre 1 byte e 8 MB ('{f.FileName}')." });
                if (!allowedMime.Contains(f.ContentType))
                    return Results.BadRequest(new { Error = $"Formato não suportado para '{f.FileName}'. Usa PDF, JPG, PNG ou WEBP." });
            }

            var openedStreams = new List<Stream>();
            try
            {
                var files = payslips
                    .Select(f =>
                    {
                        var s = f.OpenReadStream();
                        openedStreams.Add(s);
                        return (Stream: s, FileName: f.FileName);
                    })
                    .ToList();

                var result = await svc.ValidatePayslipsAsync(id, tenantId, files);
                return Results.Ok(result);
            }
            catch (ArgumentException ex) { return Results.BadRequest(new { Error = ex.Message }); }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { Error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return Results.BadRequest(new { Error = ex.Message }); }
            finally
            {
                foreach (var s in openedStreams) s.Dispose();
            }
        })
        .RequireAuthorization()
        .DisableAntiforgery()
        .RequireRateLimiting("incomeValidation");

        // DEV-ONLY: simular validação de rendimentos sem ficheiros nem chamada à IA.
        group.MapPost("/applications/{id:guid}/income-validation/simulate",
            async (Guid id, IIncomeValidationService svc, ClaimsPrincipal user, IWebHostEnvironment env) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();

            var userIdString = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (!Guid.TryParse(userIdString, out Guid tenantId))
                return Results.Unauthorized();

            try
            {
                var result = await svc.SimulateValidationAsync(id, tenantId);
                return Results.Ok(result);
            }
            catch (KeyNotFoundException ex) { return Results.NotFound(new { Error = ex.Message }); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException ex) { return Results.BadRequest(new { Error = ex.Message }); }
            catch (Exception ex) { return Results.BadRequest(new { Error = ex.Message }); }
        }).RequireAuthorization();
    }
}
