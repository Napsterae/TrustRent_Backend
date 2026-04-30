using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Services;

namespace TrustRent.Api.Endpoints;

public static class GuarantorEndpoints
{
    public static void MapGuarantorEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api").RequireAuthorization();

        // Senhorio: pedir fiador
        group.MapPost("/applications/{applicationId:guid}/guarantor/request",
            async (Guid applicationId, [FromBody] RequestGuarantorDto dto, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.RequestGuarantorAsync(applicationId, userId, dto ?? new RequestGuarantorDto())); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        // Senhorio: dispensar fiador
        group.MapPost("/applications/{applicationId:guid}/guarantor/waive",
            async (Guid applicationId, [FromBody] WaiveGuarantorDto dto, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.WaiveGuarantorAsync(applicationId, userId, dto)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        // Candidato: convidar fiador
        group.MapPost("/applications/{applicationId:guid}/guarantor/invite",
            async (Guid applicationId, [FromBody] CreateGuarantorInviteDto dto, IGuarantorService svc, HttpContext http, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                var ip = http.Connection.RemoteIpAddress?.ToString();
                return Results.Ok(await svc.InviteGuarantorAsync(applicationId, userId, dto, ip));
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        }).RequireRateLimiting("guarantorInvites");

        // Listar fiadores de uma candidatura
        group.MapGet("/applications/{applicationId:guid}/guarantors",
            async (Guid applicationId, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.GetForApplicationAsync(applicationId, userId)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        });

        // Convites pendentes para o utilizador atual
        group.MapGet("/me/guarantor-invites", async (IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            return Results.Ok(await svc.GetPendingInvitesForUserAsync(userId));
        });

        group.MapGet("/guarantors/{guarantorId:guid}",
            async (Guid guarantorId, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.GetByIdForUserAsync(guarantorId, userId)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
        });

        // Fiador: aceitar / recusar / submeter dados
        group.MapPost("/guarantors/{guarantorId:guid}/accept",
            async (Guid guarantorId, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.AcceptInviteAsync(guarantorId, userId)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        group.MapPost("/guarantors/{guarantorId:guid}/decline",
            async (Guid guarantorId, [FromBody] GuarantorDecisionDto dto, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.DeclineInviteAsync(guarantorId, userId, dto ?? new GuarantorDecisionDto())); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        group.MapPost("/guarantors/{guarantorId:guid}/submit",
            async (Guid guarantorId, HttpRequest request, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                var dto = await ReadSubmitGuarantorDataAsync(request);
                return Results.Ok(await svc.SubmitDataAsync(guarantorId, userId, dto));
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        group.MapPost("/guarantors/{guarantorId:guid}/identity/extract",
            async (Guid guarantorId, HttpRequest request, IGuarantorService svc, IGeminiDocumentService gemini, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                await svc.GetByIdForUserAsync(guarantorId, userId);
                return Results.Ok(await ExtractIdentityAsync(request, gemini));
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        group.MapPost("/guarantors/{guarantorId:guid}/address-proof/extract",
            async (Guid guarantorId, HttpRequest request, IGuarantorService svc, IGeminiDocumentService gemini, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try
            {
                await svc.GetByIdForUserAsync(guarantorId, userId);
                return Results.Ok(await ExtractAddressProofAsync(request, gemini));
            }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        // Senhorio: aprovar / rejeitar
        group.MapPost("/guarantors/{guarantorId:guid}/approve",
            async (Guid guarantorId, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.ApproveAsync(guarantorId, userId)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        group.MapPost("/guarantors/{guarantorId:guid}/reject",
            async (Guid guarantorId, [FromBody] GuarantorDecisionDto dto, IGuarantorService svc, ClaimsPrincipal user) =>
        {
            if (!TryGetUserId(user, out var userId)) return Results.Unauthorized();
            try { return Results.Ok(await svc.RejectAsync(guarantorId, userId, dto)); }
            catch (KeyNotFoundException e) { return Results.NotFound(e.Message); }
            catch (UnauthorizedAccessException) { return Results.Forbid(); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        var guest = app.MapGroup("/api/guarantor-guest");

        guest.MapGet("/{token}", async (string token, IGuarantorService svc) =>
        {
            try { return Results.Ok(await svc.GetByGuestTokenAsync(token)); }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
        });

        guest.MapPost("/{token}/accept", async (string token, IGuarantorService svc) =>
        {
            try { return Results.Ok(await svc.AcceptInviteByTokenAsync(token)); }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        guest.MapPost("/{token}/decline", async (string token, [FromBody] GuarantorDecisionDto dto, IGuarantorService svc) =>
        {
            try { return Results.Ok(await svc.DeclineInviteByTokenAsync(token, dto ?? new GuarantorDecisionDto())); }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        guest.MapPost("/{token}/submit", async (string token, HttpRequest request, IGuarantorService svc) =>
        {
            try
            {
                var dto = await ReadSubmitGuarantorDataAsync(request);
                return Results.Ok(await svc.SubmitDataByTokenAsync(token, dto));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        guest.MapPost("/{token}/identity/extract", async (string token, HttpRequest request, IGuarantorService svc, IGeminiDocumentService gemini) =>
        {
            try
            {
                await svc.GetByGuestTokenAsync(token);
                return Results.Ok(await ExtractIdentityAsync(request, gemini));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        });

        guest.MapPost("/{token}/address-proof/extract", async (string token, HttpRequest request, IGuarantorService svc, IGeminiDocumentService gemini) =>
        {
            try
            {
                await svc.GetByGuestTokenAsync(token);
                return Results.Ok(await ExtractAddressProofAsync(request, gemini));
            }
            catch (UnauthorizedAccessException) { return Results.Unauthorized(); }
            catch (InvalidOperationException e) { return Results.BadRequest(new { error = e.Message }); }
            catch (ArgumentException e) { return Results.BadRequest(new { error = e.Message }); }
        });
    }

    private static async Task<object> ExtractIdentityAsync(HttpRequest request, IGeminiDocumentService gemini)
    {
        if (!request.HasFormContentType)
            throw new ArgumentException("Envia os ficheiros da frente e verso do Cartão de Cidadão.");

        var form = await request.ReadFormAsync();
        var front = form.Files.GetFile("ccFrontDocument");
        var back = form.Files.GetFile("ccBackDocument");

        if (front == null || front.Length == 0 || back == null || back.Length == 0)
            throw new ArgumentException("É obrigatório enviar a frente e o verso do Cartão de Cidadão.");

        await using var frontStream = front.OpenReadStream();
        await using var backStream = back.OpenReadStream();
        var files = new List<(Stream Stream, string FileName)>
        {
            (frontStream, front.FileName),
            (backStream, back.FileName)
        };

        var cc = await gemini.ExtractDocumentAsync<CartaoCidadaoResponse>(files, DocumentPrompts.CartaoCidadao);
        ValidateIdentityResponse(cc);

        var fullName = $"{cc.FirstNames?.Trim()} {cc.LastNames?.Trim()}".Trim();
        if (string.IsNullOrWhiteSpace(fullName))
            fullName = cc.FullName?.Trim() ?? string.Empty;

        var nif = NormalizeExtractedNumber(cc.Nif);
        var ccNumber = NormalizeExtractedNumber(cc.CitizenCardNumber);

        if (string.IsNullOrWhiteSpace(fullName) || string.IsNullOrWhiteSpace(nif) || string.IsNullOrWhiteSpace(ccNumber))
            throw new InvalidOperationException("Não foi possível extrair todos os campos obrigatórios do Cartão de Cidadão.");

        return new
        {
            fullName,
            taxNumber = nif,
            idDocumentNumber = ccNumber,
            expiryDate = cc.ExpiryDate
        };
    }

    private static async Task<SubmitGuarantorDataDto> ReadSubmitGuarantorDataAsync(HttpRequest request)
    {
        if (!request.HasFormContentType)
        {
            return await request.ReadFromJsonAsync<SubmitGuarantorDataDto>()
                ?? throw new ArgumentException("Dados do fiador inválidos.");
        }

        var form = await request.ReadFormAsync();
        var dto = new SubmitGuarantorDataDto
        {
            FullName = form["fullName"].FirstOrDefault(),
            PhoneNumber = form["phoneNumber"].FirstOrDefault(),
            TaxNumber = form["taxNumber"].FirstOrDefault(),
            IdDocumentNumber = form["idDocumentNumber"].FirstOrDefault(),
            Address = form["address"].FirstOrDefault(),
            PostalCode = form["postalCode"].FirstOrDefault(),
            EmploymentType = form["employmentType"].FirstOrDefault() ?? "Employee",
            IncomeValidationMethod = form["incomeValidationMethod"].FirstOrDefault() ?? "Payslips",
            EmployerName = form["employerName"].FirstOrDefault(),
            EmployerNif = form["employerNif"].FirstOrDefault(),
            SimulateIdentityMatch = ReadBool(form["simulateIdentityMatch"].FirstOrDefault(), true),
            SimulateAddressMatch = ReadBool(form["simulateAddressMatch"].FirstOrDefault(), true)
        };

        var payslipFilesCount = form.Files.GetFiles("payslips").Count;
        dto.PayslipsProvidedCount = ReadNullableInt(form["payslipsProvidedCount"].FirstOrDefault()) ?? payslipFilesCount;
        dto.EmploymentStartDate = ReadNullableDate(form["employmentStartDate"].FirstOrDefault());

        return dto;
    }

    private static bool ReadBool(string? value, bool fallback)
    {
        if (string.IsNullOrWhiteSpace(value)) return fallback;
        return bool.TryParse(value, out var parsed) ? parsed : fallback;
    }

    private static int? ReadNullableInt(string? value)
        => int.TryParse(value, out var parsed) ? parsed : null;

    private static DateTime? ReadNullableDate(string? value)
        => DateTime.TryParse(value, out var parsed) ? parsed : null;

    private static void ValidateIdentityResponse(CartaoCidadaoResponse response)
    {
        if (!response.IsAuthentic)
            throw new InvalidOperationException($"Documento inválido ou potencialmente fraudulento: {response.FraudReason ?? "sem detalhe"}.");

        if (!string.Equals(response.ImageQuality, "good", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A qualidade da imagem não é suficiente para validação.");

        if (!response.AllFieldsExtracted)
            throw new InvalidOperationException("Não foi possível extrair todos os campos do Cartão de Cidadão.");
    }

    private static async Task<object> ExtractAddressProofAsync(HttpRequest request, IGeminiDocumentService gemini)
    {
        if (!request.HasFormContentType)
            throw new ArgumentException("Envia o comprovativo de morada.");

        var form = await request.ReadFormAsync();
        var document = form.Files.GetFile("addressProofDocument");

        if (document == null || document.Length == 0)
            throw new ArgumentException("É obrigatório enviar um comprovativo de morada.");

        await using var stream = document.OpenReadStream();
        var proof = await gemini.ExtractDocumentAsync<AddressProofResponse>(stream, document.FileName, DocumentPrompts.ComprovativoMorada);
        ValidateAddressProofResponse(proof);

        var address = NormalizeExtractedText(proof.Address);
        if (string.IsNullOrWhiteSpace(address))
            throw new InvalidOperationException("Não foi possível extrair uma morada válida do comprovativo.");

        return new
        {
            address,
            postalCode = NormalizePostalCode(proof.PostalCode),
            holderName = NormalizeExtractedText(proof.HolderName),
            taxNumber = NormalizeExtractedNumber(proof.Nif),
            documentType = proof.DocumentType,
            issuerName = proof.IssuerName,
            issueDate = proof.IssueDate
        };
    }

    private static void ValidateAddressProofResponse(AddressProofResponse response)
    {
        if (!response.IsAuthentic)
            throw new InvalidOperationException($"Comprovativo inválido ou potencialmente fraudulento: {response.FraudReason ?? "sem detalhe"}.");

        if (!string.Equals(response.ImageQuality, "good", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("A qualidade do comprovativo de morada não é suficiente para validação.");

        if (!response.AllFieldsExtracted)
            throw new InvalidOperationException("Não foi possível extrair todos os campos necessários do comprovativo de morada.");
    }

    private static string? NormalizeExtractedNumber(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value.Replace(" ", "").Replace("-", "").Trim();

    private static string? NormalizeExtractedText(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : System.Text.RegularExpressions.Regex.Replace(value.Trim(), "\\s+", " ");

    private static string? NormalizePostalCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        var digits = new string(value.Where(char.IsDigit).ToArray());
        if (digits.Length != 7) return value.Trim();
        return $"{digits[..4]}-{digits[4..]}";
    }

    private static bool TryGetUserId(ClaimsPrincipal user, out Guid userId)
        => Guid.TryParse(user.FindFirst(ClaimTypes.NameIdentifier)?.Value, out userId);
}
