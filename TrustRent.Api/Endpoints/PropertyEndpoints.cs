using System.Security.Claims;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;
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


        propertyGroup.MapGet("/my-properties", async (ClaimsPrincipal userClaims, IPropertyService propertyService, LeasingDbContext leasingDb) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var properties = await propertyService.GetPropertiesByLandlordAsync(userId);

                var occupiedLikeStatuses = new[]
                {
                    LeaseStatus.Active,
                    LeaseStatus.AwaitingPayment,
                    LeaseStatus.PendingLandlordSignature,
                    LeaseStatus.PendingTenantSignature,
                    LeaseStatus.AwaitingSignatures,
                    LeaseStatus.SignaturesVerified,
                    LeaseStatus.GeneratingContract
                };

                // Enriquecer com info de lease ativo
                var propertyIds = properties.Select(p => p.Id).ToList();
                var activeLeases = await leasingDb.Leases
                    .Where(l => propertyIds.Contains(l.PropertyId) && occupiedLikeStatuses.Contains(l.Status))
                    .Select(l => new { l.PropertyId, l.EndDate, l.Status })
                    .ToListAsync();

                var enriched = properties.Select(p =>
                {
                    var lease = activeLeases
                        .Where(l => l.PropertyId == p.Id)
                        .OrderByDescending(l => l.Status == LeaseStatus.Active)
                        .ThenByDescending(l => l.EndDate)
                        .FirstOrDefault();

                    return new
                    {
                        p.Id, p.Title, p.District, p.Municipality, p.Parish,
                        p.IsPublic, p.IsUnderMaintenance, p.MainImageUrl,
                        ActiveLeaseEndDate = lease?.EndDate,
                        ActiveLeaseStatus = lease?.Status.ToString()
                    };
                });

                return Results.Ok(enriched);
            }
            catch (Exception ex)
            {
                return Results.BadRequest(new { Error = ex.Message });
            }
        });

        propertyGroup.MapGet("/my-rented-properties", async (ClaimsPrincipal userClaims, IPropertyService propertyService, LeasingDbContext leasingDb) =>
        {
            try
            {
                var userId = Guid.Parse(userClaims.FindFirstValue(ClaimTypes.NameIdentifier)!);
                var properties = await propertyService.GetPropertiesByTenantAsync(userId);

                var occupiedLikeStatuses = new[]
                {
                    LeaseStatus.Active,
                    LeaseStatus.AwaitingPayment,
                    LeaseStatus.PendingLandlordSignature,
                    LeaseStatus.PendingTenantSignature,
                    LeaseStatus.AwaitingSignatures,
                    LeaseStatus.SignaturesVerified,
                    LeaseStatus.GeneratingContract
                };

                // Enriquecer com info de lease ativo
                var propertyIds = properties.Select(p => p.Id).ToList();
                var activeLeases = await leasingDb.Leases
                    .Where(l => propertyIds.Contains(l.PropertyId)
                        && l.TenantId == userId
                        && occupiedLikeStatuses.Contains(l.Status))
                    .Select(l => new { l.PropertyId, l.EndDate, l.Status })
                    .ToListAsync();

                var enriched = properties.Select(p =>
                {
                    var lease = activeLeases
                        .Where(l => l.PropertyId == p.Id)
                        .OrderByDescending(l => l.Status == LeaseStatus.Active)
                        .ThenByDescending(l => l.EndDate)
                        .FirstOrDefault();

                    return new
                    {
                        p.Id, p.Title, p.District, p.Municipality, p.Parish,
                        p.IsPublic, p.IsUnderMaintenance, p.MainImageUrl,
                        ActiveLeaseEndDate = lease?.EndDate,
                        ActiveLeaseStatus = lease?.Status.ToString()
                    };
                });

                return Results.Ok(enriched);
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
        propertyGroup.MapPut("/{id:guid}", async (Guid id, HttpRequest request, ClaimsPrincipal userClaims, IPropertyService propertyService, IStripeAccountService stripeAccountService, LeasingDbContext leasingDb) =>
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

                    // Regra de ocupação: imóvel com arrendamento em curso/finalização não pode ser publicado,
                    // exceto nas situações legais de transição (não renovação iminente ou denúncia antecipada iniciada).
                    var canPublishOccupiedProperty = await CanPublishOccupiedPropertyAsync(id, leasingDb);
                    if (!canPublishOccupiedProperty)
                    {
                        return Results.BadRequest(new
                        {
                            Error = "Não é possível publicar um imóvel ocupado enquanto o arrendamento estiver em curso. " +
                                    "Só é permitido publicar quando faltar até 14 dias para o fim e houver oposição à renovação " +
                                    "por alguma das partes, ou quando já existir um pedido de denúncia antecipada pendente."
                        });
                    }
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

        // DEV-ONLY: devolve dados de extração simulados para cada tipo de documento de imóvel,
        // sem ficheiro nem chamada à IA. Bloqueado fora de Development.
        propertyGroup.MapPost("/extract-document/simulate", (HttpRequest request, IWebHostEnvironment env) =>
        {
            if (!env.IsDevelopment())
                return Results.NotFound();

            var docType = request.Query["docType"].ToString();
            if (string.IsNullOrWhiteSpace(docType))
                docType = request.HasFormContentType ? request.Form["docType"].ToString() : string.Empty;

            DocumentExtractionResultDto result = docType switch
            {
                "caderneta" => new DocumentExtractionResultDto(
                    MatrixArticle: "1234",
                    PropertyFraction: "A",
                    ParishConcelho: "União das Freguesias de Lisboa / Lisboa"),
                "certificado" => new DocumentExtractionResultDto(
                    EnergyClass: "B",
                    EnergyCertNumber: "CE-2025-0001234"),
                "modelo2" => new DocumentExtractionResultDto(
                    AtRegistrationNumber: "AT-DEV-00000001"),
                "certidao" => new DocumentExtractionResultDto(
                    PermanentCertNumber: "PC-DEV-1234-5678-9012",
                    PermanentCertOffice: "Conservatória do Registo Predial de Lisboa"),
                "licenca" => new DocumentExtractionResultDto(
                    LicenseNumber: "LU-2024-0001",
                    LicenseDate: DateTime.UtcNow.AddYears(-5).ToString("yyyy-MM-dd"),
                    LicenseIssuer: "Câmara Municipal de Lisboa"),
                _ => null!
            };

            if (result == null)
                return Results.BadRequest(new { Error = $"Tipo de documento não suportado: '{docType}'." });

            return Results.Ok(result);
        });

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
            var currentUserId = Guid.TryParse(userIdString, out var parsedUserId) ? parsedUserId : Guid.Empty;

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

            // 5. Verificar se o utilizador pode ver dados sensíveis de documentos
            // Proprietário vê sempre; inquilino vê se tiver candidatura aceite ou arrendamento activo
            var canSeeDocuments = showFullAddress;

            // 6. Compor a resposta final (Enrichment)
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

                // Coordenadas mascaradas — só mostra exactas ao proprietário ou com visita aceite
                Latitude = showFullAddress ? property.Latitude : Math.Round(property.Latitude, 2),
                Longitude = showFullAddress ? property.Longitude : Math.Round(property.Longitude, 2),

                // Dados de documentos — visíveis ao proprietário e inquilinos com candidatura aceite ou arrendamento activo
                MatrixArticle = canSeeDocuments ? property.MatrixArticle : null,
                PropertyFraction = canSeeDocuments ? property.PropertyFraction : null,
                ParishConcelho = canSeeDocuments ? property.ParishConcelho : null,
                property.EnergyClass,
                EnergyCertificateNumber = canSeeDocuments ? property.EnergyCertificateNumber : null,
                AtRegistrationNumber = canSeeDocuments ? property.AtRegistrationNumber : null,
                PermanentCertNumber = canSeeDocuments ? property.PermanentCertNumber : null,
                PermanentCertOffice = canSeeDocuments ? property.PermanentCertOffice : null,
                UsageLicenseNumber = canSeeDocuments ? property.UsageLicenseNumber : null,
                UsageLicenseDate = canSeeDocuments ? property.UsageLicenseDate : null,
                UsageLicenseIssuer = canSeeDocuments ? property.UsageLicenseIssuer : null,

                property.Deposit,
                property.AdvanceRentMonths,
                property.CondominiumFeesPaidBy,
                property.WaterPaidBy,
                property.ElectricityPaidBy,
                property.GasPaidBy,
                property.HasOfficialContract,
                property.LeaseRegime,
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

    private static readonly LeaseStatus[] PublicationBlockingLeaseStatuses =
    {
        LeaseStatus.Active,
        LeaseStatus.AwaitingPayment,
        LeaseStatus.PendingLandlordSignature,
        LeaseStatus.PendingTenantSignature,
        LeaseStatus.AwaitingSignatures,
        LeaseStatus.SignaturesVerified,
        LeaseStatus.GeneratingContract
    };

    private static async Task<bool> CanPublishOccupiedPropertyAsync(Guid propertyId, LeasingDbContext leasingDb)
    {
        var now = DateTime.UtcNow;

        var lease = await leasingDb.Leases
            .Where(l => l.PropertyId == propertyId && PublicationBlockingLeaseStatuses.Contains(l.Status))
            .OrderByDescending(l => l.Status == LeaseStatus.Active)
            .ThenByDescending(l => l.EndDate)
            .FirstOrDefaultAsync();

        // Sem arrendamento em curso/finalização -> pode publicar.
        if (lease == null)
            return true;

        // Exceção 1: denúncia antecipada já iniciada por algum interveniente.
        var hasPendingEarlyTermination = await leasingDb.LeaseTerminationRequests
            .AnyAsync(r => r.LeaseId == lease.Id && r.Status == "Pending");

        if (hasPendingEarlyTermination)
            return true;

        // Exceção 2: até 14 dias do fim e com oposição à renovação por qualquer parte.
        var isWithinLastTwoWeeks = lease.EndDate >= now && lease.EndDate <= now.AddDays(14);
        if (isWithinLastTwoWeeks)
        {
            var hasNonRenewalResponse = await leasingDb.LeaseRenewalNotifications
                .AnyAsync(n => n.LeaseId == lease.Id
                    && (n.LandlordResponse == "Cancel" || n.TenantResponse == "Cancel"));

            if (hasNonRenewalResponse)
                return true;
        }

        return false;
    }
}
