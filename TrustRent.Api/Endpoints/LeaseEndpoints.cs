using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class LeaseEndpoints
{
    public static void MapLeaseEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/leases");

        // POST /api/leases/applications/{applicationId}/initiate
        group.MapPost("/applications/{applicationId:guid}/initiate",
            async (Guid applicationId, [FromBody] InitiateLeaseProcedureDto dto,
                   ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.InitiateLeaseProcedureAsync(applicationId, userId, dto);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/leases/{leaseId}
        group.MapGet("/{leaseId:guid}",
            async (Guid leaseId, ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.GetLeaseByIdAsync(leaseId, userId);
                    return lease is null ? Results.NotFound() : Results.Ok(lease);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // GET /api/leases/application/{applicationId}
        group.MapGet("/application/{applicationId:guid}",
            async (Guid applicationId, ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.GetLeaseByApplicationIdAsync(applicationId, userId);
                    return lease is null ? Results.NotFound() : Results.Ok(lease);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // GET /api/leases/tenant
        group.MapGet("/tenant",
            async (ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                var leases = await service.GetLeasesForTenantAsync(userId);
                return Results.Ok(leases);
            }).RequireAuthorization();

        // PUT /api/leases/{leaseId}/confirm-start-date
        group.MapPut("/{leaseId:guid}/confirm-start-date",
            async (Guid leaseId, [FromBody] ConfirmLeaseStartDateDto dto,
                   ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.ConfirmLeaseStartDateAsync(leaseId, userId, dto);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // PUT /api/leases/{leaseId}/counter-propose-date
        group.MapPut("/{leaseId:guid}/counter-propose-date",
            async (Guid leaseId, [FromBody] ConfirmLeaseStartDateDto dto,
                   ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.CounterProposeStartDateAsync(leaseId, userId, dto);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/leases/{leaseId}/request-signature
        group.MapPost("/{leaseId:guid}/request-signature",
            async (Guid leaseId, [FromBody] RequestLeaseSignatureDto dto,
                   ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.RequestSignatureAsync(leaseId, userId, dto);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/leases/{leaseId}/confirm-signature
        group.MapPost("/{leaseId:guid}/confirm-signature",
            async (Guid leaseId, [FromBody] ConfirmLeaseSignatureDto dto,
                   ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.ConfirmSignatureAsync(leaseId, userId, dto);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // POST /api/leases/{leaseId}/accept-terms
        group.MapPost("/{leaseId:guid}/accept-terms",
            async (Guid leaseId, [FromBody] AcceptLeaseTermsDto dto,
                   ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.AcceptLeaseTermsAsync(leaseId, userId, dto);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (ArgumentException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();

        // GET /api/leases/{leaseId}/signature-status
        group.MapGet("/{leaseId:guid}/signature-status",
            async (Guid leaseId, ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var status = await service.GetSignatureStatusAsync(leaseId, userId);
                    return status is null ? Results.NotFound() : Results.Ok(status);
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // GET /api/leases/{leaseId}/contract
        group.MapGet("/{leaseId:guid}/contract",
            async (Guid leaseId, ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var bytes = await service.GenerateContractAsync(leaseId, userId);
                    return Results.File(bytes, "application/pdf", $"contrato_{leaseId}.pdf");
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
                catch (FileNotFoundException e) { return Results.NotFound(e.Message); }
            }).RequireAuthorization();

        // POST /api/leases/{leaseId}/upload-signed-contract  (multipart/form-data)
        group.MapPost("/{leaseId:guid}/upload-signed-contract",
            async (Guid leaseId, HttpRequest request, ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                if (!request.HasFormContentType) return Results.BadRequest("Enviar ficheiro como multipart/form-data.");
                var form = await request.ReadFormAsync();
                var file = form.Files.GetFile("file");
                if (file is null || file.Length == 0) return Results.BadRequest("Ficheiro PDF não recebido.");
                if (!file.FileName.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                    return Results.BadRequest("Apenas ficheiros PDF são aceites.");
                if (file.Length > 50 * 1024 * 1024) // 50MB limit
                    return Results.BadRequest("O ficheiro excede o tamanho máximo permitido (50 MB).");
                try
                {
                    using var ms = new MemoryStream();
                    await file.CopyToAsync(ms);
                    var lease = await service.UploadSignedContractAsync(leaseId, userId, ms.ToArray(), file.FileName);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException e) { return Results.BadRequest(e.Message); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization().DisableAntiforgery();

        // GET /api/leases/{leaseId}/landlord-signed-contract
        group.MapGet("/{leaseId:guid}/landlord-signed-contract",
            async (Guid leaseId, ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var bytes = await service.GetLandlordSignedContractAsync(leaseId, userId);
                    if (bytes is null) return Results.NotFound("O contrato assinado pelo proprietário ainda não está disponível.");
                    return Results.File(bytes, "application/pdf", $"contrato_assinado_proprietario_{leaseId}.pdf");
                }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
            }).RequireAuthorization();

        // POST /api/leases/{leaseId}/cancel
        group.MapPost("/{leaseId:guid}/cancel",
            async (Guid leaseId, [FromBody] CancelLeaseDto dto,
                   ILeaseService service, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
                try
                {
                    var lease = await service.CancelLeaseAsync(leaseId, userId, dto);
                    return Results.Ok(lease);
                }
                catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
                catch (UnauthorizedAccessException) { return Results.Forbid(); }
                catch (InvalidOperationException e) { return Results.BadRequest(e.Message); }
            }).RequireAuthorization();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var val = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(val, out userId);
    }
}