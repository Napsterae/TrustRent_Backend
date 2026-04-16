using System.Security.Claims;
using System.Text.RegularExpressions;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Interfaces;

namespace TrustRent.Api.Endpoints;

public static class PropertyEndpoints
{
    public static void MapPropertyEndpoints(this IEndpointRouteBuilder app)
    {
        // Agrupamos os endpoints e exigimos que o utilizador esteja autenticado (Token JWT válido)
        var propertyGroup = app.MapGroup("/api/properties").RequireAuthorization();

        propertyGroup.MapPost("/", async (HttpRequest request, ClaimsPrincipal userClaims, IPropertyService propertyService, IStripeAccountService stripeAccountService) =>
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
                    HasOfficialContract = form["hasOfficialContract"] == "true",
                    //IsUnderMaintenance = form["isUnderMaintenance"] == "true",

                    // Documentos extraídos via IA
                    ParishConcelho = form["parishConcelho"].ToString(),
                    PermanentCertNumber = form["permanentCertNumber"].ToString(),
                    PermanentCertOffice = form["permanentCertOffice"].ToString(),
                    UsageLicenseNumber = form["licenseNumber"].ToString(),
                    UsageLicenseDate = form["licenseDate"].ToString(),
                    UsageLicenseIssuer = form["licenseIssuer"].ToString(),

                    // Caução e Despesas
                    Deposit = decimal.TryParse(form["deposit"], out var dep) ? dep : null,
                    AdvanceRentMonths = int.TryParse(form["advanceRentMonths"], out var advanceMonths) ? advanceMonths : 0,
                    CondominiumFeesPaidBy = form["condominiumFeesPaidBy"].ToString() is { Length: > 0 } condo ? condo : "Inquilino",
                    WaterPaidBy = form["waterPaidBy"].ToString() is { Length: > 0 } water ? water : "Inquilino",
                    ElectricityPaidBy = form["electricityPaidBy"].ToString() is { Length: > 0 } elec ? elec : "Inquilino",
                    GasPaidBy = form["gasPaidBy"].ToString() is { Length: > 0 } gas ? gas : "Inquilino",

                    // Periodicidade e Regime
                    LeaseRegime = form["leaseRegime"].ToString(),
                    AllowsRenewal = form["allowsRenewal"] == "true",
                    NonPermanentReason = form["nonPermanentReason"].ToString(),
                };

                // Validação de publicação
                if (dto.IsPublic)
                {
                    // Verificar se o proprietário tem um meio de recebimento Stripe configurado
                    var stripeAccount = await stripeAccountService.GetDefaultAccountAsync(userId);
                    if (stripeAccount == null || !stripeAccount.ChargesEnabled || !stripeAccount.PayoutsEnabled)
                        return Results.BadRequest(new { Error = "É necessário configurar um meio de recebimento de pagamentos antes de publicar o imóvel." });

                    if (string.IsNullOrEmpty(dto.LeaseRegime))
                        return Results.BadRequest(new { Error = "O regime jurídico é obrigatório para publicar o anúncio." });

                    if (dto.LeaseRegime == "NonPermanentHousing" && string.IsNullOrEmpty(dto.NonPermanentReason))
                        return Results.BadRequest(new { Error = "O motivo é obrigatório para regime não permanente." });
                }

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

                // Comodidades selecionadas
                var amenityIds = new List<Guid>();
                if (form.TryGetValue("amenityIds", out var amenityValues))
                {
                    amenityIds = amenityValues
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Select(v => Guid.Parse(v!))
                        .ToList();
                }

                // Periodicidades selecionadas
                var acceptedPeriodicities = new List<int>();
                if (form.TryGetValue("acceptedPeriodicities", out var periodicityValues))
                {
                    acceptedPeriodicities = periodicityValues
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Select(v => int.Parse(v!))
                        .ToList();
                }

                if (dto.IsPublic && !acceptedPeriodicities.Any())
                    return Results.BadRequest(new { Error = "Deve selecionar pelo menos uma periodicidade para publicar." });

                // 5. Chamar a nossa lógica de negócio (O motor que criámos no Passo 2)
                var propertyId = await propertyService.CreatePropertyAsync(userId, dto, imageFiles, imageCategories, mainImageIndex, documentFiles, amenityIds, acceptedPeriodicities);

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

        propertyGroup.MapGet("/my-rented-properties", async (ClaimsPrincipal userClaims, IPropertyService propertyService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var properties = await propertyService.GetPropertiesByTenantAsync(userId);

                return Results.Ok(properties);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        propertyGroup.MapGet("/{id:guid}/tenant-management", async (Guid id, ClaimsPrincipal userClaims, IPropertyService propertyService) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var data = await propertyService.GetTenantManagementAsync(id, userId);
                return Results.Ok(data);
            }
            catch (KeyNotFoundException ex)
            {
                return Results.NotFound(new { Error = ex.Message });
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });



        // 2. PUT: Atualizar o imóvel
        propertyGroup.MapPut("/{id:guid}", async (Guid id, HttpRequest request, ClaimsPrincipal userClaims, IPropertyService propertyService, IStripeAccountService stripeAccountService) =>
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
                    HasOfficialContract = form["hasOfficialContract"] == "true",
                    //IsUnderMaintenance = form["isUnderMaintenance"] == "true",

                    // Documentos extraídos via IA
                    ParishConcelho = form["parishConcelho"].ToString(),
                    PermanentCertNumber = form["permanentCertNumber"].ToString(),
                    PermanentCertOffice = form["permanentCertOffice"].ToString(),
                    UsageLicenseNumber = form["licenseNumber"].ToString(),
                    UsageLicenseDate = form["licenseDate"].ToString(),
                    UsageLicenseIssuer = form["licenseIssuer"].ToString(),

                    // Caução e Despesas
                    Deposit = decimal.TryParse(form["deposit"], out var dep) ? dep : null,
                    AdvanceRentMonths = int.TryParse(form["advanceRentMonths"], out var advanceMonths) ? advanceMonths : 0,
                    CondominiumFeesPaidBy = form["condominiumFeesPaidBy"].ToString() is { Length: > 0 } condo ? condo : "Inquilino",
                    WaterPaidBy = form["waterPaidBy"].ToString() is { Length: > 0 } water ? water : "Inquilino",
                    ElectricityPaidBy = form["electricityPaidBy"].ToString() is { Length: > 0 } elec ? elec : "Inquilino",
                    GasPaidBy = form["gasPaidBy"].ToString() is { Length: > 0 } gas ? gas : "Inquilino",

                    // Periodicidade e Regime
                    LeaseRegime = form["leaseRegime"].ToString(),
                    AllowsRenewal = form["allowsRenewal"] == "true",
                    NonPermanentReason = form["nonPermanentReason"].ToString(),
                };

                // Validação de publicação
                if (dto.IsPublic)
                {
                    // Verificar se o proprietário tem um meio de recebimento Stripe configurado
                    var stripeAccount = await stripeAccountService.GetAccountForPropertyAsync(id);
                    if (stripeAccount == null || !stripeAccount.ChargesEnabled || !stripeAccount.PayoutsEnabled)
                    {
                        stripeAccount = await stripeAccountService.GetDefaultAccountAsync(userId);
                    }

                    if (stripeAccount == null || !stripeAccount.ChargesEnabled || !stripeAccount.PayoutsEnabled)
                        return Results.BadRequest(new { Error = "É necessário configurar um meio de recebimento de pagamentos antes de publicar o imóvel." });

                    if (string.IsNullOrEmpty(dto.LeaseRegime))
                        return Results.BadRequest(new { Error = "O regime jurídico é obrigatório para publicar o anúncio." });

                    if (dto.LeaseRegime == "NonPermanentHousing" && string.IsNullOrEmpty(dto.NonPermanentReason))
                        return Results.BadRequest(new { Error = "O motivo é obrigatório para regime não permanente." });
                }

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

                // Comodidades selecionadas
                var amenityIds = new List<Guid>();
                if (form.TryGetValue("amenityIds", out var amenityValues))
                {
                    amenityIds = amenityValues
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Select(v => Guid.Parse(v!))
                        .ToList();
                }

                // Periodicidades selecionadas
                var acceptedPeriodicities = new List<int>();
                if (form.TryGetValue("acceptedPeriodicities", out var periodicityValues))
                {
                    acceptedPeriodicities = periodicityValues
                        .Where(v => !string.IsNullOrEmpty(v))
                        .Select(v => int.Parse(v!))
                        .ToList();
                }

                if (dto.IsPublic && !acceptedPeriodicities.Any())
                    return Results.BadRequest(new { Error = "Deve selecionar pelo menos uma periodicidade para publicar." });

                await propertyService.UpdatePropertyAsync(id, userId, dto, newImageFiles, imageCategories, retainedImageIds, mainImageIndex, mainRetainedImageId, amenityIds, acceptedPeriodicities);

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

        // GET: Obter detalhes do imóvel
        app.MapGet("/api/properties/{id:guid}", async (Guid id, IPropertyService propertyService, IUserService userService, IApplicationService applicationService, ClaimsPrincipal userClaims) =>
        {
            // 1. Identificar o utilizador (se autenticado)
            var userIdString = userClaims.FindFirstValue(ClaimTypes.NameIdentifier);
            var currentUserId = string.IsNullOrEmpty(userIdString) ? Guid.Empty : Guid.Parse(userIdString);

            // 2. Ir buscar o imóvel ao Catalog
            var property = await propertyService.GetPropertyByIdAsync(id);
            if (property is null) return Results.NotFound();

            // 3. Verificar permissões para ver a morada completa
            var showFullAddress = false;
            if (currentUserId != Guid.Empty)
            {
                if (currentUserId == property.LandlordId)
                {
                    showFullAddress = true;
                }
                else
                {
                    // Verificar se o inquilino tem uma candidatura com visita aceite ou superior
                    var tenantApps = await applicationService.GetApplicationsForTenantAsync(currentUserId);
                    var app = tenantApps.FirstOrDefault(a => a.PropertyId == id);
                    if (app != null && app.Status != "Pending" && app.Status != "VisitCounterProposed" && app.Status != "Rejected")
                    {
                        showFullAddress = true;
                    }
                }
            }

            // 4. Ir buscar o senhorio ao Identity
            var landlord = await userService.GetProfileAsync(property.LandlordId);

            // 5. Compor a resposta final (Enrichment)
            var response = new
            {
                property.Id,
                property.LandlordId,
                property.Title,
                property.Description,
                property.Price,
                property.PropertyType,
                property.Typology,
                property.Area,
                property.Rooms,
                property.Bathrooms,
                property.Floor,
                property.HasElevator,
                property.HasAirConditioning,
                property.HasGarage,
                property.AllowsPets,
                property.IsFurnished,
                property.FurnishedDescription,
                property.IsPublic,
                
                // Dados Sensíveis (Mascarados se não tiver permissão)
                Street = showFullAddress ? property.Street : "Morada visível após agendamento de visita",
                DoorNumber = showFullAddress ? property.DoorNumber : "***",
                PostalCode = showFullAddress ? property.PostalCode : "****-***",
                
                property.Parish,
                property.Municipality,
                property.District,
                property.Latitude,
                property.Longitude,
                property.MatrixArticle,
                property.PropertyFraction,
                property.ParishConcelho,
                property.EnergyClass,
                property.EnergyCertificateNumber,
                property.AtRegistrationNumber,
                property.PermanentCertNumber,
                property.PermanentCertOffice,
                property.UsageLicenseNumber,
                property.UsageLicenseDate,
                property.UsageLicenseIssuer,
                property.Deposit,
                property.AdvanceRentMonths,
                property.CondominiumFeesPaidBy,
                property.WaterPaidBy,
                property.ElectricityPaidBy,
                property.GasPaidBy,
                property.HasOfficialContract,
                property.LeaseRegime,
                property.AllowsRenewal,
                property.NonPermanentReason,
                AcceptedPeriodicities = property.AcceptedPeriodicities.Select(pp => pp.DurationMonths),
                Images = property.Images.Select(img => new {
                    img.Id,
                    img.Url,
                    img.Category,
                    img.IsMain
                }),
                Amenities = property.Amenities.Select(pa => new {
                    pa.Amenity.Id,
                    pa.Amenity.Name,
                    pa.Amenity.IconName,
                    pa.Amenity.Category
                }),
                Landlord = new
                {
                    Name = landlord?.Name ?? "Senhorio Desconhecido",
                    Avatar = landlord?.ProfilePictureUrl,
                    IsVerified = landlord?.IsIdentityVerified ?? false,
                    IsTransparent = landlord?.IsNoDebtVerified ?? false,
                    TrustScore = landlord?.TrustScore ?? 0,
                    JoinedAt = landlord?.CreatedAt
                }
            };

            return Results.Ok(response);
        });

        // GET: Lista de todas as comodidades disponíveis
        app.MapGet("/api/amenities", async (IPropertyService propertyService) =>
        {
            var amenities = await propertyService.GetAllAmenitiesAsync();
            return Results.Ok(amenities.Select(a => new
            {
                a.Id,
                a.Name,
                a.IconName,
                a.Category
            }));
        });

    }
}
