using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Models;
using TrustRent.Shared.Services;

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

        // POST /api/leases/{leaseId}/renewal-response — respond to renewal notification
        group.MapPost("/{leaseId:guid}/renewal-response",
            async (Guid leaseId, [FromBody] RenewalResponseDto dto,
                   LeasingDbContext db, IdentityDbContext identityDb,
                   INotificationService notificationService, IEmailService emailService,
                   ClaimsPrincipal user, HttpContext httpContext) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

                if (dto.Response != "Renew" && dto.Response != "Cancel")
                    return Results.BadRequest("Resposta deve ser 'Renew' ou 'Cancel'.");

                var notification = await db.LeaseRenewalNotifications
                    .FirstOrDefaultAsync(n => n.LeaseId == leaseId && !n.Processed);

                if (notification == null)
                    return Results.NotFound("Não existe notificação de renovação pendente para este contrato.");

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");

                // Proteção dos 3 anos — Art. 1096.º CC
                // Contratos de habitação permanente com duração < 3 anos não podem ser opostos à renovação
                // antes de perfazer um total de 3 anos de vigência.
                if (dto.Response == "Cancel" && lease.LeaseRegime == "PermanentHousing")
                {
                    var totalMonthsOccupied = (int)((lease.EndDate - lease.CreatedAt).TotalDays / 30.44);
                    if (totalMonthsOccupied < 36)
                    {
                        return Results.BadRequest(
                            "Nos termos do Art. 1096.º do Código Civil, contratos de habitação permanente " +
                            "com duração total inferior a 3 anos não podem ser opostos à renovação antes de " +
                            $"perfazer um total de 3 anos de vigência. O contrato atual terá {totalMonthsOccupied} meses.");
                    }
                }

                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();
                var recipientId = Guid.Empty;

                if (userId == lease.LandlordId)
                {
                    if (notification.LandlordResponse != null)
                        return Results.BadRequest("Já respondeu a esta notificação de renovação.");
                    notification.LandlordResponse = dto.Response;
                    notification.LandlordRespondedAt = DateTime.UtcNow;
                    notification.LandlordResponseIpAddress = ipAddress;
                    recipientId = lease.TenantId;
                }
                else if (userId == lease.TenantId)
                {
                    if (notification.TenantResponse != null)
                        return Results.BadRequest("Já respondeu a esta notificação de renovação.");
                    notification.TenantResponse = dto.Response;
                    notification.TenantRespondedAt = DateTime.UtcNow;
                    notification.TenantResponseIpAddress = ipAddress;
                    recipientId = lease.LandlordId;
                }
                else
                {
                    return Results.Forbid();
                }

                // Registar comunicação legal com IP e timestamp
                var communicationType = dto.Response == "Cancel" ? "NonRenewalResponse" : "RenewalResponse";
                var content = dto.Response == "Cancel"
                    ? $"O utilizador comunicou a sua intenção de NÃO RENOVAR o contrato de arrendamento (Lease ID: {leaseId})."
                    : $"O utilizador comunicou a sua intenção de RENOVAR o contrato de arrendamento (Lease ID: {leaseId}).";

                var legalLog = new LegalCommunicationLog
                {
                    LeaseId = leaseId,
                    CommunicationType = communicationType,
                    SenderId = userId,
                    RecipientId = recipientId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    SenderIpAddress = ipAddress,
                    SenderUserAgent = userAgent,
                    RenewalNotificationId = notification.Id,
                    ContentHash = ComputeSha256(content)
                };
                db.LegalCommunicationLogs.Add(legalLog);

                // Se ambas as partes responderam, processar decisão imediatamente
                if (notification.LandlordResponse != null && notification.TenantResponse != null)
                {
                    notification.Processed = true;

                    if (notification.LandlordResponse == "Renew" && notification.TenantResponse == "Renew")
                    {
                        // Ambos querem renovar — estender o contrato
                        var renewalMonths = lease.DurationMonths;

                        // Proteção dos 3 anos (Art. 1096.º CC)
                        if (lease.LeaseRegime == "PermanentHousing")
                        {
                            var totalMonthsSoFar = (int)((lease.EndDate - lease.CreatedAt).TotalDays / 30.44);
                            if (totalMonthsSoFar < 36)
                            {
                                renewalMonths = Math.Max(renewalMonths, 36 - totalMonthsSoFar);
                            }
                        }

                        lease.StartDate = lease.EndDate;
                        lease.EndDate = lease.EndDate.AddMonths(renewalMonths);
                        lease.RenewalDate = DateTime.UtcNow;
                        lease.UpdatedAt = DateTime.UtcNow;

                        await notificationService.SendNotificationAsync(
                            lease.LandlordId, "LeaseRenewed",
                            $"O contrato foi renovado até {lease.EndDate:dd/MM/yyyy}.", lease.Id);
                        await notificationService.SendNotificationAsync(
                            lease.TenantId, "LeaseRenewed",
                            $"O contrato foi renovado até {lease.EndDate:dd/MM/yyyy}.", lease.Id);
                    }
                    else
                    {
                        // Pelo menos uma parte não quer renovar — contrato termina na data prevista
                        lease.AllowsRenewal = false;
                        lease.UpdatedAt = DateTime.UtcNow;

                        var cancelledBy = notification.LandlordResponse == "Cancel" ? "senhorio" : "inquilino";
                        var nonRenewalContent = $"Decisão de não renovação do contrato de arrendamento. " +
                            $"O {cancelledBy} optou por não renovar. O contrato terminará em {lease.EndDate:dd/MM/yyyy}. " +
                            $"Nos termos do Art. 9.º do NRAU (Lei n.º 6/2006), esta comunicação tem valor legal.";

                        // Comunicação legal ao senhorio
                        db.LegalCommunicationLogs.Add(new LegalCommunicationLog
                        {
                            LeaseId = leaseId,
                            CommunicationType = "NonRenewalDecision",
                            SenderId = Guid.Empty,
                            RecipientId = lease.LandlordId,
                            Content = nonRenewalContent,
                            SentAt = DateTime.UtcNow,
                            SenderIpAddress = "system",
                            RenewalNotificationId = notification.Id,
                            ContentHash = ComputeSha256(nonRenewalContent)
                        });

                        // Comunicação legal ao inquilino
                        db.LegalCommunicationLogs.Add(new LegalCommunicationLog
                        {
                            LeaseId = leaseId,
                            CommunicationType = "NonRenewalDecision",
                            SenderId = Guid.Empty,
                            RecipientId = lease.TenantId,
                            Content = nonRenewalContent,
                            SentAt = DateTime.UtcNow,
                            SenderIpAddress = "system",
                            RenewalNotificationId = notification.Id,
                            ContentHash = ComputeSha256(nonRenewalContent)
                        });

                        await notificationService.SendNotificationAsync(
                            lease.LandlordId, "LeaseNotRenewed",
                            $"O contrato não será renovado. O {cancelledBy} optou por não renovar. Termina em {lease.EndDate:dd/MM/yyyy}.", lease.Id);
                        await notificationService.SendNotificationAsync(
                            lease.TenantId, "LeaseNotRenewed",
                            $"O contrato não será renovado. O {cancelledBy} optou por não renovar. Termina em {lease.EndDate:dd/MM/yyyy}.", lease.Id);

                        // Enviar emails de notificação de não renovação
                        var landlordUser = await identityDb.Users.FindAsync(lease.LandlordId);
                        var tenantUser = await identityDb.Users.FindAsync(lease.TenantId);
                        var emailSubject = $"Não Renovação de Contrato — Imóvel (Contrato {leaseId.ToString()[..8]})";
                        var emailBody = $"""
                            Caro(a) utilizador(a),

                            Informamos que o contrato de arrendamento não será renovado.
                            O {cancelledBy} comunicou a sua decisão de não renovação.

                            Data de término do contrato: {lease.EndDate:dd/MM/yyyy}

                            Nos termos do Art. 1081.º do Código Civil, o imóvel deve ser entregue
                            nas condições previstas no contrato até à data de término.

                            Esta comunicação foi registada para efeitos legais conforme o Art. 9.º
                            do NRAU (Lei n.º 6/2006).

                            TrustRent — Plataforma de Arrendamento
                            """;

                        if (landlordUser != null)
                            await emailService.SendEmailAsync(landlordUser.Email, emailSubject, emailBody);
                        if (tenantUser != null)
                            await emailService.SendEmailAsync(tenantUser.Email, emailSubject, emailBody);
                    }
                }

                await db.SaveChangesAsync();

                return Results.Ok(new LeaseRenewalStatusDto
                {
                    Id = notification.Id,
                    LeaseId = notification.LeaseId,
                    LandlordId = lease.LandlordId,
                    TenantId = lease.TenantId,
                    NotifiedAt = notification.NotifiedAt,
                    DeadlineDate = notification.DeadlineDate,
                    LandlordResponse = notification.LandlordResponse,
                    LandlordRespondedAt = notification.LandlordRespondedAt,
                    TenantResponse = notification.TenantResponse,
                    TenantRespondedAt = notification.TenantRespondedAt,
                    Processed = notification.Processed,
                    LandlordNoticeDays = notification.LandlordNoticeDays,
                    TenantNoticeDays = notification.TenantNoticeDays
                });
            }).RequireAuthorization();

        // GET /api/leases/{leaseId}/renewal-status — get renewal notification status
        group.MapGet("/{leaseId:guid}/renewal-status",
            async (Guid leaseId, LeasingDbContext db, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

                // Primeiro tentar notificação pendente, depois a mais recente processada
                var notification = await db.LeaseRenewalNotifications
                    .FirstOrDefaultAsync(n => n.LeaseId == leaseId && !n.Processed);

                notification ??= await db.LeaseRenewalNotifications
                    .Where(n => n.LeaseId == leaseId && n.Processed)
                    .OrderByDescending(n => n.NotifiedAt)
                    .FirstOrDefaultAsync();

                if (notification == null)
                    return Results.NotFound("Não existe notificação de renovação para este contrato.");

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound();

                if (userId != lease.LandlordId && userId != lease.TenantId)
                    return Results.Forbid();

                return Results.Ok(new LeaseRenewalStatusDto
                {
                    Id = notification.Id,
                    LeaseId = notification.LeaseId,
                    LandlordId = lease.LandlordId,
                    TenantId = lease.TenantId,
                    NotifiedAt = notification.NotifiedAt,
                    DeadlineDate = notification.DeadlineDate,
                    LandlordResponse = notification.LandlordResponse,
                    LandlordRespondedAt = notification.LandlordRespondedAt,
                    TenantResponse = notification.TenantResponse,
                    TenantRespondedAt = notification.TenantRespondedAt,
                    Processed = notification.Processed,
                    LandlordNoticeDays = notification.LandlordNoticeDays,
                    TenantNoticeDays = notification.TenantNoticeDays
                });
            }).RequireAuthorization();

        // POST /api/leases/communications/{logId}/viewed — marcar comunicação legal como visualizada (registo de IP e timestamp)
        group.MapPost("/communications/{logId:guid}/viewed",
            async (Guid logId, LeasingDbContext db, ClaimsPrincipal user, HttpContext httpContext) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

                var log = await db.LegalCommunicationLogs.FindAsync(logId);
                if (log == null) return Results.NotFound("Comunicação não encontrada.");

                if (log.RecipientId != userId) return Results.Forbid();

                if (log.ViewedAt == null)
                {
                    log.ViewedAt = DateTime.UtcNow;
                    log.ViewerIpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                    log.ViewerUserAgent = httpContext.Request.Headers.UserAgent.ToString();
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { log.Id, log.ViewedAt, log.ViewerIpAddress });
            }).RequireAuthorization();

        // POST /api/leases/communications/{logId}/acknowledge — confirmar receção da comunicação legal
        group.MapPost("/communications/{logId:guid}/acknowledge",
            async (Guid logId, LeasingDbContext db, ClaimsPrincipal user, HttpContext httpContext) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

                var log = await db.LegalCommunicationLogs.FindAsync(logId);
                if (log == null) return Results.NotFound("Comunicação não encontrada.");

                if (log.RecipientId != userId) return Results.Forbid();

                if (log.AcknowledgedAt == null)
                {
                    log.AcknowledgedAt = DateTime.UtcNow;
                    log.AcknowledgerIpAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";

                    // Marcar como visualizada também se ainda não foi
                    if (log.ViewedAt == null)
                    {
                        log.ViewedAt = DateTime.UtcNow;
                        log.ViewerIpAddress = log.AcknowledgerIpAddress;
                        log.ViewerUserAgent = httpContext.Request.Headers.UserAgent.ToString();
                    }

                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { log.Id, log.AcknowledgedAt, log.AcknowledgerIpAddress });
            }).RequireAuthorization();

        // GET /api/leases/{leaseId}/communications — listar comunicações legais de um contrato
        group.MapGet("/{leaseId:guid}/communications",
            async (Guid leaseId, LeasingDbContext db, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound();

                if (userId != lease.LandlordId && userId != lease.TenantId)
                    return Results.Forbid();

                var logs = await db.LegalCommunicationLogs
                    .Where(l => l.LeaseId == leaseId)
                    .OrderByDescending(l => l.SentAt)
                    .Select(l => new
                    {
                        l.Id,
                        l.CommunicationType,
                        l.SenderId,
                        l.RecipientId,
                        l.Content,
                        l.SentAt,
                        l.ViewedAt,
                        l.AcknowledgedAt,
                        l.EmailSent,
                        l.EmailSentAt
                    })
                    .ToListAsync();

                return Results.Ok(logs);
            }).RequireAuthorization();

        // ── Denúncia Antecipada (Art. 1098.º CC) ──

        group.MapGet("/{leaseId:guid}/early-termination-info",
            async (Guid leaseId, LeasingDbContext db, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");

                if (userId != lease.TenantId && userId != lease.LandlordId)
                    return Results.Forbid();

                var oneThirdDays = lease.DurationMonths * 30.44 / 3;
                var oneThirdDate = lease.StartDate.AddDays(oneThirdDays);
                var canTerminate = userId == lease.TenantId && DateTime.UtcNow >= oneThirdDate && lease.Status == LeaseStatus.Active;
                var noticeDays = lease.DurationMonths >= 12 ? 120 : 60;
                var earliestDate = DateTime.UtcNow.AddDays(noticeDays);

                // Calcular indemnização potencial se saísse hoje + notice
                decimal? potentialIndemnification = null;
                string? indemnificationReason = null;

                if (canTerminate)
                {
                    // Mostrar o que aconteceria se pedisse para a data mais cedo possível
                    potentialIndemnification = 0;
                }

                var hasPending = await db.LeaseTerminationRequests
                    .AnyAsync(r => r.LeaseId == leaseId && r.Status == "Pending");

                return Results.Ok(new EarlyTerminationInfoDto
                {
                    LeaseId = leaseId,
                    LeaseStartDate = lease.StartDate,
                    LeaseEndDate = lease.EndDate,
                    DurationMonths = lease.DurationMonths,
                    OneThirdDate = oneThirdDate,
                    CanTerminateNow = canTerminate,
                    RequiredNoticeDays = noticeDays,
                    EarliestTerminationDate = earliestDate,
                    MonthlyRent = lease.MonthlyRent,
                    PotentialIndemnification = potentialIndemnification,
                    IndemnificationReason = indemnificationReason,
                    HasPendingRequest = hasPending
                });
            }).RequireAuthorization();

        group.MapPost("/{leaseId:guid}/request-early-termination",
            async (Guid leaseId, [FromBody] RequestEarlyTerminationDto dto,
                   LeasingDbContext db, IdentityDbContext identityDb,
                   INotificationService notificationService, IEmailService emailService,
                   HttpContext httpContext, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");

                // Validação completa
                try
                {
                    LeaseValidator.ValidateEarlyTermination(lease, userId);
                }
                catch (InvalidOperationException ex)
                {
                    return Results.BadRequest(ex.Message);
                }
                catch (UnauthorizedAccessException ex)
                {
                    return Results.Json(new { error = ex.Message }, statusCode: 403);
                }

                // Verificar se já existe pedido pendente
                var existingRequest = await db.LeaseTerminationRequests
                    .AnyAsync(r => r.LeaseId == leaseId && r.Status == "Pending");
                if (existingRequest)
                    return Results.BadRequest("Já existe um pedido de denúncia antecipada pendente para este contrato.");

                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();

                // Cálculos legais
                var oneThirdDays = lease.DurationMonths * 30.44 / 3;
                var oneThirdDate = lease.StartDate.AddDays(oneThirdDays);
                var noticeDays = lease.DurationMonths >= 12 ? 120 : 60;
                var earliestDate = DateTime.UtcNow.AddDays(noticeDays);

                // A data proposta não pode ser antes do fim do contrato em caso de denúncia antecipada
                var proposedDate = dto.ProposedTerminationDate.ToUniversalTime();
                if (proposedDate > lease.EndDate)
                    return Results.BadRequest("A data proposta não pode ser posterior à data de término do contrato. Utilize a oposição à renovação em vez da denúncia antecipada.");

                // Calcular indemnização
                decimal? indemnification = null;
                var indemnificationRequired = false;
                string? indemnificationReason = null;

                if (proposedDate < earliestDate)
                {
                    var missingDays = (earliestDate - proposedDate).TotalDays;
                    var dailyRent = lease.MonthlyRent / 30m;
                    indemnification = Math.Round(Math.Ceiling((decimal)missingDays) * dailyRent, 2);
                    indemnificationRequired = true;
                    indemnificationReason = $"Pré-aviso insuficiente: faltam {(int)Math.Ceiling(missingDays)} dias para o mínimo legal de {noticeDays} dias. " +
                        $"Indemnização calculada: {(int)Math.Ceiling(missingDays)} dias × {dailyRent:F2}€/dia = {indemnification:F2}€";
                }

                var terminationRequest = new LeaseTerminationRequest
                {
                    LeaseId = leaseId,
                    RequestedById = userId,
                    TerminationType = "EarlyTermination",
                    Reason = dto.Reason,
                    RequiredNoticeDays = noticeDays,
                    EarliestTerminationDate = earliestDate,
                    ProposedTerminationDate = proposedDate,
                    OneThirdDate = oneThirdDate,
                    HasPassedOneThird = DateTime.UtcNow >= oneThirdDate,
                    IndemnificationAmount = indemnification,
                    IndemnificationRequired = indemnificationRequired,
                    IndemnificationReason = indemnificationReason,
                    RequesterIpAddress = ipAddress,
                    RequesterUserAgent = userAgent
                };
                db.LeaseTerminationRequests.Add(terminationRequest);

                // Criar comunicação legal
                var content = $"Denúncia antecipada do contrato de arrendamento (Art. 1098.º CC). " +
                    $"O inquilino comunicou a sua intenção de terminar o contrato em {proposedDate:dd/MM/yyyy}. " +
                    $"Pré-aviso legal: {noticeDays} dias. " +
                    (indemnificationRequired
                        ? $"Indemnização devida: {indemnification:F2}€. "
                        : "Sem indemnização (pré-aviso cumprido). ") +
                    $"Motivo: {dto.Reason}";

                var legalLog = new LegalCommunicationLog
                {
                    LeaseId = leaseId,
                    CommunicationType = "EarlyTerminationNotice",
                    SenderId = userId,
                    RecipientId = lease.LandlordId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    SenderIpAddress = ipAddress,
                    SenderUserAgent = userAgent,
                    ContentHash = ComputeSha256(content)
                };
                db.LegalCommunicationLogs.Add(legalLog);

                await db.SaveChangesAsync();

                // Notificação ao senhorio
                await notificationService.SendNotificationAsync(
                    lease.LandlordId, "EarlyTermination",
                    $"O inquilino solicitou a denúncia antecipada do contrato. Data proposta de saída: {proposedDate:dd/MM/yyyy}.",
                    lease.Id);

                // Email ao senhorio
                var landlordUser = await identityDb.Users.FindAsync(lease.LandlordId);
                if (landlordUser?.Email != null)
                {
                    await emailService.SendEmailAsync(
                        landlordUser.Email,
                        $"Denúncia Antecipada — Contrato {leaseId.ToString()[..8]}",
                        $"O inquilino comunicou a sua intenção de terminar antecipadamente o contrato.\n\n" +
                        $"Data proposta: {proposedDate:dd/MM/yyyy}\n" +
                        $"Pré-aviso legal: {noticeDays} dias\n" +
                        (indemnificationRequired ? $"Indemnização: {indemnification:F2}€\n" : "") +
                        $"Motivo: {dto.Reason}\n\n" +
                        "Esta comunicação tem valor legal nos termos do Art. 9.º do NRAU.");
                }

                return Results.Ok(new EarlyTerminationResultDto
                {
                    TerminationRequestId = terminationRequest.Id,
                    ProposedTerminationDate = proposedDate,
                    EarliestTerminationDate = earliestDate,
                    RequiredNoticeDays = noticeDays,
                    IndemnificationAmount = indemnification,
                    IndemnificationRequired = indemnificationRequired,
                    IndemnificationReason = indemnificationReason,
                    Status = "Pending"
                });
            }).RequireAuthorization();

        group.MapGet("/{leaseId:guid}/termination-requests",
            async (Guid leaseId, LeasingDbContext db, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");
                if (userId != lease.TenantId && userId != lease.LandlordId)
                    return Results.Forbid();

                var requests = await db.LeaseTerminationRequests
                    .Where(r => r.LeaseId == leaseId)
                    .OrderByDescending(r => r.RequestedAt)
                    .Select(r => new
                    {
                        r.Id,
                        r.TerminationType,
                        r.Reason,
                        r.RequestedAt,
                        r.RequiredNoticeDays,
                        r.EarliestTerminationDate,
                        r.ProposedTerminationDate,
                        r.OneThirdDate,
                        r.IndemnificationAmount,
                        r.IndemnificationRequired,
                        r.IndemnificationReason,
                        r.Status,
                        r.ProcessedAt
                    })
                    .ToListAsync();

                return Results.Ok(requests);
            }).RequireAuthorization();

        // ── Atualização de Renda (Art. 1077.º CC + Art. 24.º NRAU) ──

        group.MapGet("/{leaseId:guid}/rent-increase-info",
            async (Guid leaseId, LeasingDbContext db, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");
                if (userId != lease.TenantId && userId != lease.LandlordId)
                    return Results.Forbid();

                // Verificar último aumento
                var lastIncrease = await db.RentIncreaseRequests
                    .Where(r => r.LeaseId == leaseId && r.Applied)
                    .OrderByDescending(r => r.ProcessedAt)
                    .FirstOrDefaultAsync();

                var lastIncreaseDate = lastIncrease?.ProcessedAt;
                var referenceDate = lastIncreaseDate ?? lease.StartDate;
                var oneYearAfter = referenceDate.AddYears(1);
                var canIncreaseNow = lease.Status == LeaseStatus.Active
                    && userId == lease.LandlordId
                    && DateTime.UtcNow >= oneYearAfter;

                string? cannotReason = null;
                if (lease.Status != LeaseStatus.Active)
                    cannotReason = "O contrato não está ativo.";
                else if (userId != lease.LandlordId)
                    cannotReason = "Apenas o senhorio pode solicitar atualização de renda.";
                else if (DateTime.UtcNow < oneYearAfter)
                    cannotReason = $"A atualização só é possível após {oneYearAfter:dd/MM/yyyy} (1 ano desde o início ou última atualização).";

                var hasPending = await db.RentIncreaseRequests
                    .AnyAsync(r => r.LeaseId == leaseId && (r.Status == "Pending" || r.Status == "Contested"));
                if (hasPending)
                {
                    canIncreaseNow = false;
                    cannotReason = "Já existe um pedido de aumento de renda pendente.";
                }

                // Coeficiente atual
                var currentCoeff = RentIncreaseRequest.GetCurrentCoefficient();
                var currentYear = DateTime.UtcNow.Year;
                var proposedRent = RentIncreaseRequest.CalculateNewRent(lease.MonthlyRent, currentCoeff);
                var increasePercentage = Math.Round((currentCoeff - 1) * 100, 4);

                // Acumulação (se não aumentou nos últimos 3 anos)
                var lastIncreaseYear = lastIncrease?.CoefficientYear ?? lease.StartDate.Year;
                var yearsWithout = currentYear - lastIncreaseYear;
                var canAccumulate = yearsWithout > 1;
                decimal? accCoeff = null;
                decimal? accRent = null;
                decimal? accPercentage = null;
                string? accDetails = null;

                if (canAccumulate)
                {
                    var (coefficient, details) = RentIncreaseRequest.GetAccumulatedCoefficient(lastIncreaseYear);
                    accCoeff = coefficient;
                    accRent = RentIncreaseRequest.CalculateNewRent(lease.MonthlyRent, coefficient);
                    accPercentage = Math.Round((coefficient - 1) * 100, 4);
                    accDetails = details;
                }

                return Results.Ok(new RentIncreaseInfoDto
                {
                    LeaseId = leaseId,
                    CurrentRent = lease.MonthlyRent,
                    CurrentCoefficient = currentCoeff,
                    CoefficientYear = currentYear,
                    ProposedRent = proposedRent,
                    IncreasePercentage = increasePercentage,
                    IncreaseAmount = proposedRent - lease.MonthlyRent,
                    CanIncreaseNow = canIncreaseNow,
                    CannotIncreaseReason = cannotReason,
                    EarliestIncreaseDate = canIncreaseNow ? null : oneYearAfter,
                    CanAccumulate = canAccumulate,
                    AccumulatedCoefficient = accCoeff,
                    AccumulatedProposedRent = accRent,
                    AccumulatedIncreasePercentage = accPercentage,
                    AccumulatedDetails = accDetails,
                    HasPendingRequest = hasPending,
                    LastIncreaseDate = lastIncreaseDate
                });
            }).RequireAuthorization();

        group.MapPost("/{leaseId:guid}/request-rent-increase",
            async (Guid leaseId, [FromBody] RequestRentIncreaseDto dto,
                   LeasingDbContext db, IdentityDbContext identityDb,
                   INotificationService notificationService, IEmailService emailService,
                   HttpContext httpContext, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");
                if (userId != lease.LandlordId)
                    return Results.Json(new { error = "Apenas o senhorio pode solicitar atualização de renda." }, statusCode: 403);
                if (lease.Status != LeaseStatus.Active)
                    return Results.BadRequest("Apenas contratos ativos podem ter a renda atualizada.");

                // Verificar 1 ano desde último aumento ou início
                var lastIncrease = await db.RentIncreaseRequests
                    .Where(r => r.LeaseId == leaseId && r.Applied)
                    .OrderByDescending(r => r.ProcessedAt)
                    .FirstOrDefaultAsync();

                var referenceDate = lastIncrease?.ProcessedAt ?? lease.StartDate;
                if (DateTime.UtcNow < referenceDate.AddYears(1))
                    return Results.BadRequest($"A atualização só é possível após {referenceDate.AddYears(1):dd/MM/yyyy} (1 ano desde o início ou última atualização).");

                // Verificar pedido pendente
                var hasPending = await db.RentIncreaseRequests
                    .AnyAsync(r => r.LeaseId == leaseId && (r.Status == "Pending" || r.Status == "Contested"));
                if (hasPending)
                    return Results.BadRequest("Já existe um pedido de aumento de renda pendente.");

                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();

                // Calcular coeficiente
                decimal coefficient;
                string details;
                bool accumulated = false;

                if (dto.UseAccumulated)
                {
                    var lastYear = lastIncrease?.CoefficientYear ?? lease.StartDate.Year;
                    (coefficient, details) = RentIncreaseRequest.GetAccumulatedCoefficient(lastYear);
                    accumulated = DateTime.UtcNow.Year - lastYear > 1;
                }
                else
                {
                    coefficient = RentIncreaseRequest.GetCurrentCoefficient();
                    details = $"Coeficiente {DateTime.UtcNow.Year}: {coefficient}";
                }

                var newRent = RentIncreaseRequest.CalculateNewRent(lease.MonthlyRent, coefficient);
                var percentage = Math.Round((coefficient - 1) * 100, 4);
                var effectiveDate = DateTime.UtcNow.AddDays(30);
                var contestDeadline = DateTime.UtcNow.AddDays(30);

                var request = new RentIncreaseRequest
                {
                    LeaseId = leaseId,
                    RequestedById = userId,
                    CurrentRent = lease.MonthlyRent,
                    ProposedRent = newRent,
                    IncreasePercentage = percentage,
                    CoefficientApplied = coefficient,
                    CoefficientYear = DateTime.UtcNow.Year,
                    AccumulatedCoefficients = accumulated,
                    AccumulatedDetails = accumulated ? details : null,
                    EffectiveDate = effectiveDate,
                    ContestationDeadline = contestDeadline,
                    RequesterIpAddress = ipAddress,
                    RequesterUserAgent = userAgent
                };
                db.RentIncreaseRequests.Add(request);

                // Comunicação legal
                var content = $"Atualização de renda comunicada pelo senhorio (Art. 1077.º CC / Art. 24.º NRAU). " +
                    $"Renda atual: {lease.MonthlyRent:F2}€. Nova renda proposta: {newRent:F2}€ " +
                    $"(aumento de {percentage}%, coeficiente {coefficient}). " +
                    $"Data de entrada em vigor: {effectiveDate:dd/MM/yyyy}. " +
                    $"Prazo para contestação: {contestDeadline:dd/MM/yyyy}. " +
                    (accumulated ? $"Coeficientes acumulados: {details}." : $"Coeficiente {DateTime.UtcNow.Year}: {coefficient}.");

                db.LegalCommunicationLogs.Add(new LegalCommunicationLog
                {
                    LeaseId = leaseId,
                    CommunicationType = "RentIncreaseNotice",
                    SenderId = userId,
                    RecipientId = lease.TenantId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    SenderIpAddress = ipAddress,
                    SenderUserAgent = userAgent,
                    ContentHash = ComputeSha256(content)
                });

                await db.SaveChangesAsync();

                // Notificação ao inquilino
                await notificationService.SendNotificationAsync(
                    lease.TenantId, "RentIncrease",
                    $"O senhorio comunicou uma atualização de renda de {lease.MonthlyRent:F2}€ para {newRent:F2}€ (aumento de {percentage}%). " +
                    $"Entra em vigor a {effectiveDate:dd/MM/yyyy}. Tem até {contestDeadline:dd/MM/yyyy} para contestar.",
                    lease.Id);

                // Email ao inquilino
                var tenantUser = await identityDb.Users.FindAsync(lease.TenantId);
                if (tenantUser?.Email != null)
                {
                    await emailService.SendEmailAsync(
                        tenantUser.Email,
                        $"Atualização de Renda — Contrato {leaseId.ToString()[..8]}",
                        $"O senhorio comunicou uma atualização da renda do seu arrendamento.\n\n" +
                        $"Renda atual: {lease.MonthlyRent:F2}€\n" +
                        $"Nova renda: {newRent:F2}€ (aumento de {percentage}%)\n" +
                        $"Coeficiente aplicado: {coefficient}\n" +
                        $"Data de entrada em vigor: {effectiveDate:dd/MM/yyyy}\n\n" +
                        $"Tem até {contestDeadline:dd/MM/yyyy} para contestar esta atualização.\n\n" +
                        "Esta comunicação tem valor legal nos termos do Art. 24.º do NRAU.");
                }

                return Results.Ok(new RentIncreaseResultDto
                {
                    RequestId = request.Id,
                    CurrentRent = lease.MonthlyRent,
                    NewRent = newRent,
                    IncreasePercentage = percentage,
                    CoefficientApplied = coefficient,
                    EffectiveDate = effectiveDate,
                    ContestationDeadline = contestDeadline,
                    Status = "Pending"
                });
            }).RequireAuthorization();

        group.MapPost("/{leaseId:guid}/rent-increase/{requestId:guid}/contest",
            async (Guid leaseId, Guid requestId, [FromBody] ContestRentIncreaseDto dto,
                   LeasingDbContext db, IdentityDbContext identityDb,
                   INotificationService notificationService, IEmailService emailService,
                   HttpContext httpContext, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");
                if (userId != lease.TenantId)
                    return Results.Json(new { error = "Apenas o inquilino pode contestar a atualização de renda." }, statusCode: 403);

                var request = await db.RentIncreaseRequests.FindAsync(requestId);
                if (request == null || request.LeaseId != leaseId)
                    return Results.NotFound("Pedido de aumento não encontrado.");
                if (request.Status != "Pending")
                    return Results.BadRequest("Este pedido já não pode ser contestado.");
                if (DateTime.UtcNow > request.ContestationDeadline)
                    return Results.BadRequest($"O prazo para contestação expirou em {request.ContestationDeadline:dd/MM/yyyy}.");
                if (string.IsNullOrWhiteSpace(dto.Reason))
                    return Results.BadRequest("O motivo da contestação é obrigatório.");

                var ipAddress = httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
                var userAgent = httpContext.Request.Headers.UserAgent.ToString();

                request.Status = "Contested";
                request.Contested = true;
                request.ContestedAt = DateTime.UtcNow;
                request.ContestationReason = dto.Reason;

                // Comunicação legal
                var content = $"Contestação de atualização de renda pelo inquilino (Art. 1077.º CC). " +
                    $"O inquilino contesta o aumento de {request.CurrentRent:F2}€ para {request.ProposedRent:F2}€. " +
                    $"Motivo: {dto.Reason}";

                db.LegalCommunicationLogs.Add(new LegalCommunicationLog
                {
                    LeaseId = leaseId,
                    CommunicationType = "RentIncreaseContestation",
                    SenderId = userId,
                    RecipientId = lease.LandlordId,
                    Content = content,
                    SentAt = DateTime.UtcNow,
                    SenderIpAddress = ipAddress,
                    SenderUserAgent = userAgent,
                    ContentHash = ComputeSha256(content)
                });

                await db.SaveChangesAsync();

                // Notificação ao senhorio
                await notificationService.SendNotificationAsync(
                    lease.LandlordId, "RentIncreaseContested",
                    $"O inquilino contestou a atualização de renda. Motivo: {dto.Reason}",
                    lease.Id);

                // Email ao senhorio
                var landlordUser = await identityDb.Users.FindAsync(lease.LandlordId);
                if (landlordUser?.Email != null)
                {
                    await emailService.SendEmailAsync(
                        landlordUser.Email,
                        $"Contestação de Atualização de Renda — Contrato {leaseId.ToString()[..8]}",
                        $"O inquilino contestou a atualização de renda proposta.\n\n" +
                        $"Aumento contestado: {request.CurrentRent:F2}€ → {request.ProposedRent:F2}€\n" +
                        $"Motivo da contestação: {dto.Reason}\n\n" +
                        "A atualização de renda fica suspensa até resolução da contestação.");
                }

                return Results.Ok(new { message = "Contestação registada com sucesso.", requestId = request.Id, status = request.Status });
            }).RequireAuthorization();

        group.MapGet("/{leaseId:guid}/rent-increase-history",
            async (Guid leaseId, LeasingDbContext db, ClaimsPrincipal user) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                var lease = await db.Leases.FindAsync(leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");
                if (userId != lease.TenantId && userId != lease.LandlordId)
                    return Results.Forbid();

                var history = await db.RentIncreaseRequests
                    .Where(r => r.LeaseId == leaseId)
                    .OrderByDescending(r => r.RequestedAt)
                    .Select(r => new
                    {
                        r.Id,
                        r.CurrentRent,
                        r.ProposedRent,
                        r.IncreasePercentage,
                        r.CoefficientApplied,
                        r.CoefficientYear,
                        r.AccumulatedCoefficients,
                        r.AccumulatedDetails,
                        r.RequestedAt,
                        r.EffectiveDate,
                        r.ContestationDeadline,
                        r.Status,
                        r.Contested,
                        r.ContestedAt,
                        r.ContestationReason,
                        r.Applied,
                        r.ProcessedAt
                    })
                    .ToListAsync();

                return Results.Ok(history);
            }).RequireAuthorization();

        // PUT /api/leases/{leaseId}/tax-registration — Registar contrato nas Finanças com extração automática
        group.MapPut("/{leaseId:guid}/tax-registration",
            async (Guid leaseId, IFormFile document, LeasingDbContext db, ClaimsPrincipal user, IGeminiDocumentService geminiService, IUserService userService) =>
            {
                if (!TryGetUserId(user, out var userId))
                    return Results.Unauthorized();

                if (document == null || document.Length == 0)
                    return Results.BadRequest("É obrigatório enviar o comprovativo de registo da AT.");

                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png", ".webp" };
                var extension = Path.GetExtension(document.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(extension))
                    return Results.BadRequest("Formato de ficheiro inválido. Usa PDF, JPG, PNG ou WEBP.");

                var lease = await db.Leases
                    .Include(l => l.History)
                    .FirstOrDefaultAsync(l => l.Id == leaseId);
                if (lease == null) return Results.NotFound("Arrendamento não encontrado.");
                if (userId != lease.LandlordId) return Results.Forbid();
                if (lease.IsRegisteredWithTaxAuthority)
                    return Results.BadRequest("Este contrato já se encontra registado nas Finanças.");

                var landlord = await userService.GetProfileAsync(userId);
                if (landlord == null)
                    return Results.NotFound("Utilizador não encontrado.");
                if (string.IsNullOrWhiteSpace(landlord.Nif) || string.IsNullOrWhiteSpace(landlord.Name))
                    return Results.BadRequest("Para validar o comprovativo, o senhorio deve ter Nome e NIF preenchidos no perfil.");

                TaxRegistrationValidationResponse extraction;
                await using (var stream = document.OpenReadStream())
                {
                    extraction = await geminiService.ExtractDocumentAsync<TaxRegistrationValidationResponse>(
                        stream,
                        document.FileName,
                        DocumentPrompts.RegistoAtTaxValidation);
                }

                var validationError = ValidateTaxRegistrationExtraction(extraction, landlord.Name, landlord.Nif);
                if (validationError != null)
                    return Results.BadRequest(validationError);

                var extractedReference = extraction.AtRegistrationNumber?.Trim();
                if (string.IsNullOrWhiteSpace(extractedReference))
                    return Results.BadRequest("Não foi possível extrair a referência do comprovativo da AT.");

                lease.IsRegisteredWithTaxAuthority = true;
                lease.TaxRegistrationDate = DateTime.UtcNow;
                lease.TaxRegistrationReference = extractedReference;
                lease.UpdatedAt = DateTime.UtcNow;

                lease.History.Add(new LeaseHistory
                {
                    LeaseId = lease.Id,
                    ActorId = userId,
                    Action = "TaxRegistered",
                    Message = $"Contrato registado nas Finanças (validação automática com comprovativo). Referência: {extractedReference}"
                });

                await db.SaveChangesAsync();
                return Results.Ok(new
                {
                    message = "Registo nas Finanças confirmado com sucesso após validação automática do comprovativo.",
                    reference = extractedReference
                });
            }).RequireAuthorization();
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
    {
        var val = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        return Guid.TryParse(val, out userId);
    }

    private static string? ValidateTaxRegistrationExtraction(TaxRegistrationValidationResponse extraction, string landlordName, string landlordNif)
    {
        if (!extraction.IsAuthentic)
            return "O comprovativo enviado não passou na verificação de autenticidade.";

        var qualityMessage = extraction.ImageQuality switch
        {
            "blurry" => "A imagem está desfocada. Envia um comprovativo mais nítido.",
            "dark" => "A imagem está escura. Envia um comprovativo com melhor iluminação.",
            "cropped" => "O comprovativo está cortado. Envia o documento completo.",
            "unreadable" => "Não foi possível ler o comprovativo. Envia o PDF original ou uma imagem mais nítida.",
            _ => null
        };

        if (qualityMessage != null)
            return qualityMessage;

        if (!extraction.AllFieldsExtracted)
            return "Não foi possível extrair todos os campos obrigatórios do comprovativo da AT.";

        var extractedNif = NormalizeDigits(extraction.LandlordNif);
        var expectedNif = NormalizeDigits(landlordNif);
        if (string.IsNullOrWhiteSpace(extractedNif) || extractedNif != expectedNif)
            return "O NIF extraído do comprovativo não corresponde ao NIF do proprietário.";

        if (!NamesMatch(extraction.LandlordName, landlordName))
            return "O nome no comprovativo não corresponde ao nome do proprietário.";

        return null;
    }

    private static string NormalizeDigits(string? input)
        => new((input ?? string.Empty).Where(char.IsDigit).ToArray());

    private static bool NamesMatch(string? extractedName, string expectedName)
    {
        if (string.IsNullOrWhiteSpace(extractedName) || string.IsNullOrWhiteSpace(expectedName))
            return false;

        var normalizedExtracted = NormalizeName(extractedName);
        var normalizedExpected = NormalizeName(expectedName);

        var expectedParts = normalizedExpected.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (expectedParts.Length == 0)
            return false;

        var firstName = expectedParts.First();
        var lastName = expectedParts.Last();

        var hasFirstName = normalizedExtracted.Contains(firstName, StringComparison.OrdinalIgnoreCase);
        var hasLastName = normalizedExtracted.Contains(lastName, StringComparison.OrdinalIgnoreCase);

        return hasFirstName && hasLastName;
    }

    private static string NormalizeName(string value)
    {
        var decomposed = value.Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);
        foreach (var ch in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                builder.Append(ch);
        }

        return builder.ToString().Normalize(NormalizationForm.FormC).ToUpperInvariant().Trim();
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}