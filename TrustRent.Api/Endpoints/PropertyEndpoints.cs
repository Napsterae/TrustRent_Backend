using System.Security.Claims;
using System.Text.RegularExpressions;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;

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
                    District = form["district"].ToString(),
                    Municipality = form["municipality"].ToString(),
                    Parish = form["parish"].ToString(),
                    DoorNumber = form["doorNumber"].ToString(),
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

                    IsPublic = form["isPublic"] == "true",
                    //IsUnderMaintenance = form["isUnderMaintenance"] == "true",
                };

                var mainImageIndex = int.TryParse(form["mainImageIndex"], out var mii) ? mii : 0;

                // 4. Apanhar as listas de ficheiros (Imagens e Documentos)
                var imageFiles = form.Files.GetFiles("images")
                    .Select(f => new FileDto(f.OpenReadStream(), f.FileName)).ToList();

                var documentFiles = form.Files.GetFiles("documents")
                    .Select(f => new FileDto(f.OpenReadStream(), f.FileName)).ToList();

                var imageCategories = form["imageCategories"]
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Select(c => c!)
                    .ToList();

                // 5. Chamar a nossa lógica de negócio (O motor que criámos no Passo 2)
                var propertyId = await propertyService.CreatePropertyAsync(userId, dto, imageFiles, imageCategories, mainImageIndex, documentFiles);

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

        // 1. GET: Obter detalhes do imóvel
        propertyGroup.MapGet("/{id:guid}", async (Guid id, IPropertyService propertyService) =>
        {
            var property = await propertyService.GetPropertyByIdAsync(id);
            return property is not null ? Results.Ok(property) : Results.NotFound();
        });

        // 2. PUT: Atualizar o imóvel
        propertyGroup.MapPut("/{id:guid}", async (Guid id, HttpRequest request, ClaimsPrincipal userClaims, IPropertyService propertyService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var form = await request.ReadFormAsync();

                // Dica: Usamos a mesma estrutura de DTO que usaste no POST!
                var dto = new CreatePropertyDto
                {
                    Title = form["title"].ToString(),
                    Description = form["description"].ToString(),
                    PropertyType = form["propertyType"].ToString(),
                    Typology = form["typology"].ToString(),
                    Floor = form["floor"].ToString(),
                    Street = form["street"].ToString(),
                    PostalCode = form["postalCode"].ToString(),
                    District = form["district"].ToString(),
                    Municipality = form["municipality"].ToString(),
                    Parish = form["parish"].ToString(),
                    DoorNumber = form["doorNumber"].ToString(),
                    FurnishedDescription = form["furnishedDescription"].ToString(),
                    Price = decimal.TryParse(form["price"], out var p) ? p : 0,
                    Area = decimal.TryParse(form["area"], out var a) ? a : 0,
                    Rooms = int.TryParse(form["rooms"], out var r) ? r : 0,
                    Bathrooms = int.TryParse(form["bathrooms"], out var b) ? b : 0,
                    Latitude = double.TryParse(form["latitude"], out var lat) ? lat : 0,
                    Longitude = double.TryParse(form["longitude"], out var lon) ? lon : 0,
                    HasElevator = form["hasElevator"] == "true",
                    HasAirConditioning = form["hasAirConditioning"] == "true",
                    HasGarage = form["hasGarage"] == "true",
                    AllowsPets = form["allowsPets"] == "true",
                    IsFurnished = form["isFurnished"] == "true",
                    IsPublic = form["isPublic"] == "true",
                    //IsUnderMaintenance = form["isUnderMaintenance"] == "true",
                };

                // Apanhar apenas as NOVAS imagens adicionadas na edição
                var newImageFiles = form.Files.GetFiles("images")
                    .Select(f => new FileDto(f.OpenReadStream(), f.FileName)).ToList();

                var imageCategories = form["imageCategories"]
                    .Where(c => !string.IsNullOrEmpty(c))
                    .Select(c => c!)
                    .ToList();

                var mainImageIndex = int.TryParse(form["mainImageIndex"], out var mii) ? mii : -1;
                var mainRetainedImageId = Guid.TryParse(form["mainRetainedImageId"], out var mrid) ? mrid : (Guid?)null;

                var retainedImageIds = new List<Guid>();
                if (form.TryGetValue("retainedImageIds", out var ids))
                {
                    retainedImageIds = ids.Select(id => Guid.Parse(id!.ToString())).ToList();
                }

                await propertyService.UpdatePropertyAsync(id, userId, dto, newImageFiles, imageCategories, retainedImageIds, mainImageIndex, mainRetainedImageId);

                return Results.Ok(new { Message = "Imóvel atualizado com sucesso!" });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        propertyGroup.MapPost("/extract-document", async (HttpRequest request, IDocumentExtractionService extractionService) =>
        {
            try
            {
                var form = await request.ReadFormAsync();
                var file = form.Files.FirstOrDefault();
                var docType = form["docType"].ToString();

                if (file == null || file.Length == 0) return Results.BadRequest(new { Error = "Nenhum ficheiro recebido." });

                using var stream = file.OpenReadStream();

                var resultData = await extractionService.ExtractDataAsync(stream, file.FileName, docType);

                return Results.Ok(resultData);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        }).DisableAntiforgery();

        app.MapGet("/api/properties", async ([AsParameters] PropertySearchQuery query, IPropertyService propertyService) =>
        {
            try
            {
                var result = await propertyService.SearchPropertiesAsync(query);
                return Results.Ok(result);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

    }
}