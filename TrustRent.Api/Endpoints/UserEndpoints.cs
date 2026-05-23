using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrustRent.Api.Services;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Identity.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class UserEndpoints
{
    public static void MapAuthUserEndpoints(this IEndpointRouteBuilder app)
    {
        // Agrupamos todas as rotas sob "/api/auth"
        var userGroup = app.MapGroup("/api/user").RequireAuthorization();

        userGroup.MapGet("/profile", async (ClaimsPrincipal userClaims, IUserService userService) =>
        {
            var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await userService.GetProfileDtoAsync(userId);
            return user is not null ? Results.Ok(user) : Results.NotFound();
        });

        // Public profile (no auth needed for now, or just limit to authenticated users? They are in the userGroup which requires auth, which is fine)
        userGroup.MapGet("/{id:guid}/public", async (Guid id, ClaimsPrincipal userClaims, IUserService userService) =>
        {
            var viewerUserId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var profile = await userService.GetPublicProfileAsync(id, viewerUserId);
            return profile is not null ? Results.Ok(profile) : Results.NotFound();
        });

        userGroup.MapPut("/profile", async (ClaimsPrincipal userClaims, [FromBody] UpdateProfileDto request, IUserService userService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await userService.UpdateProfileAsync(userId, request);
                var profile = await userService.GetProfileDtoAsync(userId);
                return Results.Ok(new
                {
                    Message = "Perfil atualizado com sucesso.",
                    PhoneVerificationRequired = profile is not null
                        && (!string.IsNullOrWhiteSpace(profile.PendingPhoneNumber)
                            || (!string.IsNullOrWhiteSpace(profile.PhoneNumber) && !profile.IsPhoneNumberVerified)),
                    IsPhoneNumberVerified = profile is not null
                        && string.IsNullOrWhiteSpace(profile.PendingPhoneNumber)
                        && profile.IsPhoneNumberVerified,
                    PhoneContactPlatform = profile?.PendingPhoneContactPlatform ?? profile?.PhoneContactPlatform
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        userGroup.MapPost("/phone-verification/request", async (ClaimsPrincipal userClaims, [FromBody] RequestPhoneVerificationDto? request, IUserService userService, HttpContext ctx) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await userService.RequestPhoneNumberVerificationAsync(
                    userId,
                    request,
                    ctx.Connection.RemoteIpAddress?.ToString(),
                    ctx.Request.Headers.UserAgent.ToString(),
                    ctx.RequestAborted);

                return Results.Ok(new
                {
                    result.Platform,
                    result.Message,
                    result.ExpiresAtUtc,
                    result.DeepLinkUrl,
                    result.BotUsername,
                    result.AwaitingContactShare
                });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        userGroup.MapGet("/phone-verification/status", async (ClaimsPrincipal userClaims, IUserService userService, HttpContext ctx) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await userService.GetPhoneVerificationStatusAsync(userId, ctx.RequestAborted);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        userGroup.MapPost("/phone-verification/confirm", async (ClaimsPrincipal userClaims, [FromBody] ConfirmPhoneVerificationRequest request, IUserService userService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await userService.VerifyPhoneNumberAsync(userId, request.Code);
                return Results.Ok(new { Message = "Número de telemóvel validado com sucesso." });
            }
            catch (UnauthorizedAccessException)
            {
                return Results.Json(new { Error = "Código inválido ou expirado." }, statusCode: 401);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        userGroup.MapPut("/notification-preferences", async (ClaimsPrincipal userClaims, [FromBody] UpdateNotificationPreferencesDto request, IUserService userService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await userService.UpdateNotificationPreferencesAsync(userId, request);
                return Results.Ok(new { Message = "Preferências de notificação atualizadas com sucesso." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        userGroup.MapPost("/avatar", async (ClaimsPrincipal userClaims, IFormFile file, IUserService userService) =>
        {
            try
            {
                if (file == null || file.Length == 0) return Results.BadRequest(new { Error = "Nenhuma imagem enviada." });

                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);

                using var stream = file.OpenReadStream();
                var url = await userService.UpdateAvatarAsync(userId, stream, file.FileName);

                return Results.Ok(new { Message = "Foto atualizada.", ProfilePictureUrl = url });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        userGroup.MapPost("/verify-documents", async (ClaimsPrincipal userClaims,
            IFormFile? ccFrontDocument, IFormFile? ccBackDocument, IFormFile? noDebtDocument, IFormFile? addressProofDocument, IUserService userService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);

                using var ccFrontStream = ccFrontDocument?.OpenReadStream();
                using var ccBackStream = ccBackDocument?.OpenReadStream();
                using var noDebtStream = noDebtDocument?.OpenReadStream();
                using var addressProofStream = addressProofDocument?.OpenReadStream();

                var result = await userService.VerifyDocumentsAsync(userId,
                    ccFrontStream, ccFrontDocument?.FileName,
                    ccBackStream, ccBackDocument?.FileName,
                    noDebtStream, noDebtDocument?.FileName,
                    addressProofStream, addressProofDocument?.FileName);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        // Simula validação do Cartão de Cidadão sem chamar a IA quando as simulações estão activas.
        userGroup.MapPost("/verify-documents/simulate-cc", async (
            ClaimsPrincipal userClaims,
            IUserService userService,
            IWebHostEnvironment env,
            IStagingAccessService stagingAccessService) =>
        {
            if (!await StagingSimulationPolicy.IsEnabledAsync(env, stagingAccessService))
                return Results.NotFound();

            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await userService.SimulateVerifyCitizenCardAsync(userId);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // Simula validação da Certidão de Não Dívida quando as simulações estão activas.
        userGroup.MapPost("/verify-documents/simulate-no-debt", async (
            ClaimsPrincipal userClaims,
            IUserService userService,
            IWebHostEnvironment env,
            IStagingAccessService stagingAccessService) =>
        {
            if (!await StagingSimulationPolicy.IsEnabledAsync(env, stagingAccessService))
                return Results.NotFound();

            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var result = await userService.SimulateVerifyNoDebtAsync(userId);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        userGroup.MapPut("/security", () =>
            Results.Json(new { Error = "A conta Wekaza usa login por código enviado por email. Passwords deixaram de estar disponíveis." }, statusCode: 410));
    }
}

public record UpdatePasswordRequest(string CurrentPassword, string NewPassword);
public record ConfirmPhoneVerificationRequest(string Code);