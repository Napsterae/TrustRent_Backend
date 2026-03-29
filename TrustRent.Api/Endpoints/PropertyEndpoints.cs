using System.Security.Claims;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class PropertyEndpoints
{
    public static void MapPropertyEndpoints(this IEndpointRouteBuilder app)
    {
        // Agrupamos os endpoints e exigimos que o utilizador esteja autenticado (Token JWT válido)
        var propertyGroup = app.MapGroup("/api/properties").RequireAuthorization();

        propertyGroup.MapPost("/", async (HttpRequest request, ClaimsPrincipal userClaims, IPropertyService propertyService) =>
        {
            try
            {
                // 1. Identificar quem está a fazer o pedido
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);

                // 2. Ler o FormData enviado pelo React
                var form = await request.ReadFormAsync();

                // 3. Mapear os dados de texto para o nosso DTO
                // Usamos form["chave"] porque foi assim que fizeste o formData.append("chave", valor) no React
                var dto = new CreatePropertyDto
                {
                    Title = form["title"].ToString(),
                    Description = form["description"].ToString(),
                    PropertyType = form["propertyType"].ToString(),
                    Typology = form["typology"].ToString(),
                    Floor = form["floor"].ToString(),
                    Street = form["street"].ToString(),
                    PostalCode = form["postalCode"].ToString(),
                    City = form["city"].ToString(),
                    FurnishedDescription = form["furnishedDescription"].ToString(),

                    // Conversões seguras de números (se falhar, mete 0)
                    Price = decimal.TryParse(form["price"], out var p) ? p : 0,
                    Area = decimal.TryParse(form["area"], out var a) ? a : 0,
                    Rooms = int.TryParse(form["rooms"], out var r) ? r : 0,
                    Bathrooms = int.TryParse(form["bathrooms"], out var b) ? b : 0,
                    Latitude = double.TryParse(form["latitude"], out var lat) ? lat : 0,
                    Longitude = double.TryParse(form["longitude"], out var lon) ? lon : 0,

                    // Conversões de booleanos (o React envia como strings "true" ou "false")
                    HasElevator = form["hasElevator"] == "true",
                    HasAirConditioning = form["hasAirConditioning"] == "true",
                    HasGarage = form["hasGarage"] == "true",
                    AllowsPets = form["allowsPets"] == "true",
                    IsFurnished = form["isFurnished"] == "true",
                };

                // 4. Apanhar as listas de ficheiros (Imagens e Documentos)
                var imageFiles = form.Files.GetFiles("images")
                    .Select(f => new FileDto(f.OpenReadStream(), f.FileName)).ToList();

                var documentFiles = form.Files.GetFiles("documents")
                    .Select(f => new FileDto(f.OpenReadStream(), f.FileName)).ToList();

                // 5. Chamar a nossa lógica de negócio (O motor que criámos no Passo 2)
                var propertyId = await propertyService.CreatePropertyAsync(userId, dto, imageFiles, documentFiles);

                return Results.Ok(new
                {
                    Message = "Imóvel criado com sucesso!",
                    PropertyId = propertyId
                });
            }
            catch (Exception ex)
            {
                // Se algo falhar (ex: erro na conversão ou no upload da Cloud), devolvemos o erro ao React
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery(); // Necessário para aceitar multipart/form-data nas Minimal APIs


        propertyGroup.MapGet("/my-properties", async (ClaimsPrincipal userClaims, IPropertyService propertyService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var properties = await propertyService.GetPropertiesByLandlordAsync(userId);

                return Results.Ok(properties);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

    }
}