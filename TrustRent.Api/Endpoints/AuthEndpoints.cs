using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Services;

namespace TrustRent.Api.Endpoints;

public static class AuthEndpoints
{
    // Método de extensão para mapear as rotas no Program.cs
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        // Agrupamos todas as rotas sob "/api/auth"
        var group = app.MapGroup("/api/auth");

        // Endpoint de Registo
        group.MapPost("/register", async ([FromBody] RegisterRequest request, IAuthService authService) =>
        {
            try
            {
                var token = await authService.RegisterAsync(request.Name, request.Email, request.Password);
                return Results.Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        // Endpoint de Login
        group.MapPost("/login", async ([FromBody] LoginRequest request, IAuthService authService) =>
        {
            try
            {
                var token = await authService.LoginAsync(request.Email, request.Password);
                return Results.Ok(new { Token = token });
            }
            catch (Exception ex)
            {
                return Results.Json(new { Error = ex.Message }, statusCode: 401);
            }
        });     
    }
}



// Os teus DTOs (Data Transfer Objects) continuam a existir aqui no fundo, de forma simples
public record RegisterRequest(string Name, string Email, string Password);
public record LoginRequest(string Email, string Password);