using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
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
                return Results.Ok(new { Message = "Perfil atualizado com sucesso." });
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
            IFormFile? ccFrontDocument, IFormFile? ccBackDocument, IFormFile? noDebtDocument, IUserService userService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);

                using var ccFrontStream = ccFrontDocument?.OpenReadStream();
                using var ccBackStream = ccBackDocument?.OpenReadStream();
                using var noDebtStream = noDebtDocument?.OpenReadStream();

                var result = await userService.VerifyDocumentsAsync(userId,
                    ccFrontStream, ccFrontDocument?.FileName,
                    ccBackStream, ccBackDocument?.FileName,
                    noDebtStream, noDebtDocument?.FileName);

                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        userGroup.MapPut("/security", async (ClaimsPrincipal userClaims, [FromBody] UpdatePasswordRequest request, IUserService userService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                await userService.UpdatePasswordAsync(userId, request.CurrentPassword, request.NewPassword);
                return Results.Ok(new { Message = "Password atualizada com sucesso." });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });
    }
}

public record UpdatePasswordRequest(string CurrentPassword, string NewPassword);