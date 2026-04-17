using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Identity.Seeds;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Seeds;

public static class CatalogSeeder
{
    // IDs fixos para propriedades "Core" para manter consistência nos testes manuais
    public static readonly Guid Property1Id = Guid.Parse("aaaa1111-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property2Id = Guid.Parse("aaaa2222-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property3Id = Guid.Parse("aaaa3333-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property4Id = Guid.Parse("aaaa4444-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property5Id = Guid.Parse("aaaa5555-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property6Id = Guid.Parse("aaaa6666-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property7Id = Guid.Parse("aaaa7777-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property8Id = Guid.Parse("aaaa8888-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property9Id = Guid.Parse("aaaa9999-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    public static readonly Guid LeaseActiveApplication1Id = Guid.Parse("cccc1111-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid LeaseActiveApplication2Id = Guid.Parse("cccc2222-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid PendingApplicationId = Guid.Parse("cccc3333-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid VisitCounterProposedApplicationId = Guid.Parse("cccc4444-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid InterestConfirmedApplicationId = Guid.Parse("cccc5555-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid RejectedApplicationId = Guid.Parse("cccc6666-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid ContractPendingApplicationId = Guid.Parse("cccc7777-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid AwaitingPaymentApplicationId = Guid.Parse("cccc8888-cccc-cccc-cccc-cccccccccccc");
    public static readonly Guid LeaseStartDateProposedApplicationId = Guid.Parse("cccc9999-cccc-cccc-cccc-cccccccccccc");

    private static readonly Guid Lease1Id = Guid.Parse("bbbb1111-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Lease2Id = Guid.Parse("bbbb2222-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Lease3Id = Guid.Parse("bbbb3333-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Lease4Id = Guid.Parse("bbbb4444-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Lease5Id = Guid.Parse("bbbb5555-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    private static readonly Guid WifiAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000001");
    private static readonly Guid EquippedKitchenAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000002");
    private static readonly Guid WashingMachineAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000003");
    private static readonly Guid AirConditioningAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000005");
    private static readonly Guid PoolAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000009");
    private static readonly Guid AlarmAmenityId = Guid.Parse("a0000000-0000-0000-0000-00000000000f");
    private static readonly Guid PetsAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000010");
    private static readonly Guid SupermarketAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000011");
    private static readonly Guid TransportAmenityId = Guid.Parse("a0000000-0000-0000-0000-000000000012");

    public static async Task SeedAsync(CatalogDbContext context)
    {
        // Se já temos as propriedades core, assumimos que o seed já correu
        if (await context.Properties.AnyAsync(p => p.Id == Property1Id))
        {
            var count = await context.Properties.CountAsync();
            Console.WriteLine($"[SEED] Catalog: Já existem {count} imóveis. A ignorar.");
            return;
        }

        var userIds = IdentitySeeder.AllGeneratedUserIds;
        if (userIds.Count == 0)
        {
            // Fallback se por acaso a lista estiver vazia (não deve acontecer se correr em ordem)
            userIds = new List<Guid> { IdentitySeeder.LandlordId, IdentitySeeder.Landlord2Id, IdentitySeeder.TenantId, IdentitySeeder.Tenant2Id };
        }

        var random = new Random(42); // Seed fixa para os imóveis também
        var properties = new List<Property>();

        var adjectives = new[] { "Acolhedor", "Moderno", "Espacoso", "Elegante", "Rustico", "Luminoso", "Central", "Premium" };
        var imageUrls = new[] {
            "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267",
            "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
            "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2",
            "https://images.unsplash.com/photo-1484154218962-a197022b5858",
            "https://images.unsplash.com/photo-1493809842364-78817add7ffb",
            "https://images.unsplash.com/photo-1502005229762-cf1b2da7c5d6",
            "https://images.unsplash.com/photo-1527359443443-84a18accb60d"
        };
        var nonPermanentReasons = new[]
        {
            "Estadia profissional temporaria em Portugal.",
            "Programa academico com duracao limitada.",
            "Projeto internacional com termo definido.",
            "Alojamento sazonal de media duracao."
        };

        // 1. ADICIONAR AS 9 PROPRIEDADES CORE (Com IDs fixos)
        properties.Add(CreateProperty(
            id: Property1Id,
            landlordId: IdentitySeeder.LandlordId,
            tenantId: null,
            title: "T2 Renovado no Chiado com Vista Rio",
            description: "Apartamento premium no coracao de Lisboa, pronto para visitas e com termos de arrendamento completos.",
            price: 1200m,
            propertyType: "Apartamento",
            typology: "T2",
            area: 85,
            bathrooms: 1,
            district: "Lisboa",
            municipality: "Lisboa",
            parish: "Santa Maria Maior",
            street: "Rua Garrett",
            doorNumber: "10",
            postalCode: "1200-204",
            floor: "3",
            latitude: 38.7105,
            longitude: -9.1448,
            isPublic: true,
            createdAt: DateTime.UtcNow.AddDays(-45),
            leaseRegime: LeaseRegime.PermanentHousing,
            acceptedPeriodicities: new[] { 12, 24, 36 },
            deposit: 2400m,
            advanceRentMonths: 1,
            allowsRenewal: true,
            hasOfficialContract: true,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Inquilino",
            amenityIds: new[] { WifiAmenityId, EquippedKitchenAmenityId, AirConditioningAmenityId, TransportAmenityId },
            imageUrls: imageUrls,
            random: random,
            matrixArticle: "U-1234",
            propertyFraction: "A",
            parishConcelho: "Santa Maria Maior / Lisboa",
            energyClass: "A+",
            energyCertificateNumber: "CERT-987654",
            atRegistrationNumber: "AT-987654321",
            permanentCertNumber: "1234-5678-9012",
            permanentCertOffice: "Conservatoria de Lisboa",
            usageLicenseNumber: "LU-456/2010",
            usageLicenseDate: "2010-05-15",
            usageLicenseIssuer: "Camara Municipal de Lisboa"));

        properties.Add(CreateProperty(
            id: Property2Id,
            landlordId: IdentitySeeder.LandlordId,
            tenantId: null,
            title: "T1 Moderno em Arroios",
            description: "Apartamento moderno ideal para profissionais, com renda acessivel e custos iniciais transparentes.",
            price: 850m,
            propertyType: "Apartamento",
            typology: "T1",
            area: 50,
            bathrooms: 1,
            district: "Lisboa",
            municipality: "Lisboa",
            parish: "Arroios",
            street: "Rua Almirante Reis",
            doorNumber: "45",
            postalCode: "1000-002",
            floor: "2",
            latitude: 38.7251,
            longitude: -9.1346,
            isPublic: true,
            createdAt: DateTime.UtcNow.AddDays(-31),
            leaseRegime: LeaseRegime.PermanentHousing,
            acceptedPeriodicities: new[] { 12, 18, 24 },
            deposit: 850m,
            advanceRentMonths: 0,
            allowsRenewal: true,
            hasOfficialContract: true,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Nao aplicavel",
            amenityIds: new[] { WifiAmenityId, WashingMachineAmenityId, TransportAmenityId, SupermarketAmenityId },
            imageUrls: imageUrls,
            random: random,
            matrixArticle: "U-5678",
            propertyFraction: "E",
            parishConcelho: "Arroios / Lisboa",
            energyClass: "B",
            energyCertificateNumber: "CERT-123456",
            atRegistrationNumber: "AT-123456789",
            permanentCertNumber: "9012-3456-7890",
            permanentCertOffice: "Conservatoria de Lisboa",
            usageLicenseNumber: "LU-123/2015",
            usageLicenseDate: "2015-08-20",
            usageLicenseIssuer: "Camara Municipal de Lisboa"));

        properties.Add(CreateProperty(
            id: Property3Id,
            landlordId: IdentitySeeder.Landlord2Id,
            tenantId: null,
            title: "T3 Familiar em Cascais com Jardim",
            description: "Moradia espacosa perto da praia, pensada para estadias temporarias de media duracao.",
            price: 1800m,
            propertyType: "Moradia",
            typology: "T3",
            area: 150,
            bathrooms: 2,
            district: "Lisboa",
            municipality: "Cascais",
            parish: "Cascais e Estoril",
            street: "Avenida Marginal",
            doorNumber: "105",
            postalCode: "2750-001",
            floor: "Moradia",
            latitude: 38.6979,
            longitude: -9.4215,
            isPublic: true,
            createdAt: DateTime.UtcNow.AddDays(-26),
            leaseRegime: LeaseRegime.NonPermanentHousing,
            acceptedPeriodicities: new[] { 3, 6, 9 },
            deposit: 1800m,
            advanceRentMonths: 2,
            allowsRenewal: false,
            hasOfficialContract: false,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Inquilino",
            nonPermanentReason: "Estadia profissional temporaria em Portugal.",
            amenityIds: new[] { WifiAmenityId, PoolAmenityId, PetsAmenityId, AlarmAmenityId },
            imageUrls: imageUrls,
            random: random));

        properties.Add(CreateProperty(
            id: Property4Id,
            landlordId: IdentitySeeder.Landlord2Id,
            tenantId: null,
            title: "Loft T0 na Ribeira do Porto",
            description: "Loft luminoso com vista Douro, ideal para uma estadia temporaria urbana.",
            price: 950m,
            propertyType: "Apartamento",
            typology: "T0",
            area: 45,
            bathrooms: 1,
            district: "Porto",
            municipality: "Porto",
            parish: "Cedofeita, Santo Ildefonso, Se, Miragaia, Sao Nicolau e Vitoria",
            street: "Rua das Flores",
            doorNumber: "20",
            postalCode: "4050-262",
            floor: "4",
            latitude: 41.1414,
            longitude: -8.6148,
            isPublic: true,
            createdAt: DateTime.UtcNow.AddDays(-19),
            leaseRegime: LeaseRegime.NonPermanentHousing,
            acceptedPeriodicities: new[] { 1, 3, 6 },
            deposit: 950m,
            advanceRentMonths: 1,
            allowsRenewal: false,
            hasOfficialContract: false,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Nao aplicavel",
            nonPermanentReason: "Programa academico com duracao limitada.",
            amenityIds: new[] { WifiAmenityId, EquippedKitchenAmenityId, TransportAmenityId },
            imageUrls: imageUrls,
            random: random));

        properties.Add(CreateProperty(
            id: Property5Id,
            landlordId: IdentitySeeder.LandlordId,
            tenantId: IdentitySeeder.TenantId,
            title: "Apartamento de Luxo nas Torres do Colombo",
            description: "Imovel com arrendamento ativo e contrato oficial, usado para testar tenant management e pagamentos.",
            price: 2500m,
            propertyType: "Apartamento",
            typology: "T3",
            area: 160,
            bathrooms: 2,
            district: "Lisboa",
            municipality: "Lisboa",
            parish: "Benfica",
            street: "Avenida Lusiada",
            doorNumber: "1",
            postalCode: "1500-392",
            floor: "15",
            latitude: 38.7508,
            longitude: -9.1805,
            isPublic: false,
            createdAt: DateTime.UtcNow.AddMonths(-5),
            leaseRegime: LeaseRegime.PermanentHousing,
            acceptedPeriodicities: new[] { 12, 24 },
            deposit: 5000m,
            advanceRentMonths: 2,
            allowsRenewal: true,
            hasOfficialContract: true,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Inquilino",
            amenityIds: new[] { WifiAmenityId, EquippedKitchenAmenityId, AirConditioningAmenityId, AlarmAmenityId },
            imageUrls: imageUrls,
            random: random));

        properties.Add(CreateProperty(
            id: Property6Id,
            landlordId: IdentitySeeder.Landlord2Id,
            tenantId: IdentitySeeder.Tenant2Id,
            title: "Moradia T2 em Tavira com Piscina",
            description: "Imovel com arrendamento ativo de media duracao, com historico para tickets e pagamentos.",
            price: 1100m,
            propertyType: "Moradia",
            typology: "T2",
            area: 90,
            bathrooms: 2,
            district: "Faro",
            municipality: "Tavira",
            parish: "Santa Maria e Santiago",
            street: "Rua Direita",
            doorNumber: "22",
            postalCode: "8800-001",
            floor: "Moradia",
            latitude: 37.1259,
            longitude: -7.6483,
            isPublic: false,
            createdAt: DateTime.UtcNow.AddMonths(-4),
            leaseRegime: LeaseRegime.NonPermanentHousing,
            acceptedPeriodicities: new[] { 6, 9 },
            deposit: 1100m,
            advanceRentMonths: 0,
            allowsRenewal: false,
            hasOfficialContract: false,
            condoPaidBy: "Nao aplicavel",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Inquilino",
            nonPermanentReason: "Alojamento sazonal de media duracao.",
            amenityIds: new[] { WifiAmenityId, PoolAmenityId, PetsAmenityId, SupermarketAmenityId },
            imageUrls: imageUrls,
            random: random));

        properties.Add(CreateProperty(
            id: Property7Id,
            landlordId: IdentitySeeder.LandlordId,
            tenantId: IdentitySeeder.Tenant2Id,
            title: "T2 Novo em Alvalade com Varanda",
            description: "Imovel reservado para um lease em fase de assinatura do contrato oficial.",
            price: 1450m,
            propertyType: "Apartamento",
            typology: "T2",
            area: 78,
            bathrooms: 2,
            district: "Lisboa",
            municipality: "Lisboa",
            parish: "Alvalade",
            street: "Avenida da Igreja",
            doorNumber: "88",
            postalCode: "1700-239",
            floor: "5",
            latitude: 38.7486,
            longitude: -9.1469,
            isPublic: false,
            createdAt: DateTime.UtcNow.AddDays(-16),
            leaseRegime: LeaseRegime.PermanentHousing,
            acceptedPeriodicities: new[] { 12, 24 },
            deposit: 1450m,
            advanceRentMonths: 1,
            allowsRenewal: true,
            hasOfficialContract: true,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Inquilino",
            amenityIds: new[] { WifiAmenityId, EquippedKitchenAmenityId, WashingMachineAmenityId, TransportAmenityId },
            imageUrls: imageUrls,
            random: random));

        properties.Add(CreateProperty(
            id: Property8Id,
            landlordId: IdentitySeeder.Landlord2Id,
            tenantId: IdentitySeeder.TenantId,
            title: "T1 em Braga Centro com Lugar de Garagem",
            description: "Imovel reservado com processo em fase de pagamento inicial.",
            price: 780m,
            propertyType: "Apartamento",
            typology: "T1",
            area: 58,
            bathrooms: 1,
            district: "Braga",
            municipality: "Braga",
            parish: "Braga (Sao Vicente)",
            street: "Rua do Souto",
            doorNumber: "14",
            postalCode: "4700-327",
            floor: "1",
            latitude: 41.5503,
            longitude: -8.4201,
            isPublic: false,
            createdAt: DateTime.UtcNow.AddDays(-11),
            leaseRegime: LeaseRegime.PermanentHousing,
            acceptedPeriodicities: new[] { 12 },
            deposit: 780m,
            advanceRentMonths: 1,
            allowsRenewal: true,
            hasOfficialContract: false,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Nao aplicavel",
            amenityIds: new[] { WifiAmenityId, WashingMachineAmenityId, TransportAmenityId, SupermarketAmenityId },
            imageUrls: imageUrls,
            random: random));

        properties.Add(CreateProperty(
            id: Property9Id,
            landlordId: IdentitySeeder.Landlord2Id,
            tenantId: IdentitySeeder.Tenant2Id,
            title: "T2 em Aveiro com Escritorio",
            description: "Imovel reservado onde a data de inicio de arrendamento ainda esta em negociacao.",
            price: 980m,
            propertyType: "Apartamento",
            typology: "T2",
            area: 72,
            bathrooms: 1,
            district: "Aveiro",
            municipality: "Aveiro",
            parish: "Gloria e Vera Cruz",
            street: "Rua de Coimbra",
            doorNumber: "36",
            postalCode: "3810-156",
            floor: "3",
            latitude: 40.6405,
            longitude: -8.6538,
            isPublic: false,
            createdAt: DateTime.UtcNow.AddDays(-8),
            leaseRegime: LeaseRegime.PermanentHousing,
            acceptedPeriodicities: new[] { 12, 24 },
            deposit: 980m,
            advanceRentMonths: 0,
            allowsRenewal: true,
            hasOfficialContract: true,
            condoPaidBy: "Senhorio",
            waterPaidBy: "Inquilino",
            electricityPaidBy: "Inquilino",
            gasPaidBy: "Inquilino",
            amenityIds: new[] { WifiAmenityId, EquippedKitchenAmenityId, TransportAmenityId },
            imageUrls: imageUrls,
            random: random));

        // 2. GERAR MAIS 61 PROPRIEDADES (Total 70)
        var cities = new[] { "Lisboa", "Porto", "Coimbra", "Braga", "Faro", "Aveiro", "Setúbal", "Viseu", "Évora", "Leiria" };
        var types = new[] { "Apartamento", "Moradia", "Quarto" };
        var typologies = new[] { "T0", "T1", "T2", "T3", "T4" };

        for (int i = 0; i < 61; i++)
        {
            var city = cities[random.Next(cities.Length)];
            var type = types[random.Next(types.Length)];
            var typo = type == "Quarto" ? "Quarto" : typologies[random.Next(typologies.Length)];
            var adj = adjectives[random.Next(adjectives.Length)];
            var price = random.Next(4, 25) * 100;
            var regime = random.NextDouble() > 0.30 ? LeaseRegime.PermanentHousing : LeaseRegime.NonPermanentHousing;
            var acceptedPeriodicities = GetAcceptedPeriodicitiesFor(regime, random);
            var location = GetLocationForCity(city, random);
            var landlordId = userIds[random.Next(userIds.Count)];
            var isOfficialContract = random.NextDouble() > 0.35;
            var depositMultiplier = random.Next(0, 3);
            var advanceRentMonths = random.Next(0, 3);
            
            var p = CreateProperty(
                id: Guid.NewGuid(),
                landlordId: landlordId,
                tenantId: null,
                title: $"{typo} {adj} em {city}",
                description: $"Excelente {type.ToLower()} {adj.ToLower()} situado em zona calma de {city}, com termos contratuais completos e pronto para candidatura.",
                price: price,
                propertyType: type,
                typology: typo,
                area: random.Next(35, 200),
                bathrooms: random.Next(1, 4),
                district: city,
                municipality: city,
                parish: $"Centro de {city}",
                street: $"Rua {adj} de {city}",
                doorNumber: random.Next(1, 200).ToString(),
                postalCode: $"{random.Next(1000, 9000)}-{random.Next(100, 999)}",
                floor: type == "Moradia" ? "Moradia" : random.Next(0, 8).ToString(),
                latitude: location.latitude,
                longitude: location.longitude,
                isPublic: true,
                createdAt: DateTime.UtcNow.AddDays(-random.Next(1, 60)),
                leaseRegime: regime,
                acceptedPeriodicities: acceptedPeriodicities,
                deposit: Math.Min(price * depositMultiplier, price * 2),
                advanceRentMonths: advanceRentMonths,
                allowsRenewal: regime == LeaseRegime.PermanentHousing || random.NextDouble() > 0.70,
                hasOfficialContract: isOfficialContract,
                condoPaidBy: type == "Apartamento" ? (random.NextDouble() > 0.55 ? "Senhorio" : "Inquilino") : "Nao aplicavel",
                waterPaidBy: "Inquilino",
                electricityPaidBy: "Inquilino",
                gasPaidBy: type == "Quarto" ? "Incluido" : "Inquilino",
                nonPermanentReason: regime == LeaseRegime.NonPermanentHousing ? nonPermanentReasons[random.Next(nonPermanentReasons.Length)] : null,
                amenityIds: GetRandomAmenitySelection(random),
                imageUrls: imageUrls,
                random: random);

            properties.Add(p);
        }

        // 4. GERAR CANDIDATURAS
        var applications = new List<Application>();

        applications.Add(CreateApplication(
            LeaseActiveApplication1Id,
            Property5Id,
            IdentitySeeder.TenantId,
            "Pretendo mudar-me rapidamente e aceito os termos financeiros propostos.",
            ApplicationStatus.LeaseActive,
            createdAt: DateTime.UtcNow.AddMonths(-4),
            durationMonths: 12,
            wantsVisit: false,
            proposedDates: Array.Empty<DateTime>(),
            finalVisitDate: null,
            landlordProposedDate: null,
            leaseId: Lease1Id,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", "Candidatura submetida com todos os documentos validados.", app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.LandlordId, "Candidatura Aprovada (Contrato)", "Perfil aprovado apos analise documental.", null, app.CreatedAt.AddDays(2)),
                NewHistory(app.Id, IdentitySeeder.LandlordId, "Pagamento Confirmado", "Pagamento inicial confirmado e arrendamento ativado.", null, app.CreatedAt.AddDays(9))
            }));

        applications.Add(CreateApplication(
            LeaseActiveApplication2Id,
            Property6Id,
            IdentitySeeder.Tenant2Id,
            "Procuro uma moradia calma para uma estadia temporaria de media duracao.",
            ApplicationStatus.LeaseActive,
            createdAt: DateTime.UtcNow.AddMonths(-3),
            durationMonths: 9,
            wantsVisit: false,
            proposedDates: Array.Empty<DateTime>(),
            finalVisitDate: null,
            landlordProposedDate: null,
            leaseId: Lease2Id,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", "Candidatura submetida para estadia temporaria.", app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.Landlord2Id, "Candidatura Aprovada (Contrato)", "Arrendamento temporario aprovado.", null, app.CreatedAt.AddDays(3)),
                NewHistory(app.Id, IdentitySeeder.Landlord2Id, "Arrendamento Ativado", "Todos os termos foram aceites e o lease esta ativo.", null, app.CreatedAt.AddDays(7))
            }));

        applications.Add(CreateApplication(
            PendingApplicationId,
            Property1Id,
            IdentitySeeder.Tenant2Id,
            "Tenho disponibilidade imediata e gostava de agendar visita ainda esta semana.",
            ApplicationStatus.Pending,
            createdAt: DateTime.UtcNow.AddDays(-5),
            durationMonths: 12,
            wantsVisit: true,
            proposedDates: new[] { DateTime.UtcNow.AddDays(2).Date.AddHours(18), DateTime.UtcNow.AddDays(4).Date.AddHours(11) },
            finalVisitDate: null,
            landlordProposedDate: null,
            leaseId: null,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt)
            }));

        applications.Add(CreateApplication(
            VisitCounterProposedApplicationId,
            Property1Id,
            IdentitySeeder.TenantId,
            "Gosto bastante da localizacao e queria discutir detalhes da visita.",
            ApplicationStatus.VisitCounterProposed,
            createdAt: DateTime.UtcNow.AddDays(-7),
            durationMonths: 24,
            wantsVisit: true,
            proposedDates: new[] { DateTime.UtcNow.AddDays(1).Date.AddHours(9), DateTime.UtcNow.AddDays(3).Date.AddHours(14) },
            finalVisitDate: null,
            landlordProposedDate: DateTime.UtcNow.AddDays(5).Date.AddHours(17),
            leaseId: null,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.LandlordId, "Senhorio Contra-Propos", "Prefiro receber-te num horario de fim de tarde.", app.LandlordProposedDate?.ToString("O"), app.CreatedAt.AddDays(1))
            }));

        applications.Add(CreateApplication(
            InterestConfirmedApplicationId,
            Property2Id,
            IdentitySeeder.Tenant2Id,
            "O apartamento cumpre exatamente o que procuro para um contrato de longo prazo.",
            ApplicationStatus.InterestConfirmed,
            createdAt: DateTime.UtcNow.AddDays(-10),
            durationMonths: 18,
            wantsVisit: true,
            proposedDates: new[] { DateTime.UtcNow.AddDays(-7).Date.AddHours(10), DateTime.UtcNow.AddDays(-6).Date.AddHours(16) },
            finalVisitDate: DateTime.UtcNow.AddDays(-4).Date.AddHours(18),
            landlordProposedDate: null,
            leaseId: null,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.LandlordId, "Senhorio Aceitou Data do Inquilino", "Visita confirmada.", app.FinalVisitDate?.ToString("O"), app.CreatedAt.AddDays(2)),
                NewHistory(app.Id, app.TenantId, "Inquilino Confirmou Interesse", "Pretendo avancar com o arrendamento apos a visita.", null, app.CreatedAt.AddDays(6))
            }));

        applications.Add(CreateApplication(
            RejectedApplicationId,
            Property3Id,
            IdentitySeeder.TenantId,
            "Tenho flexibilidade para o periodo proposto e disponibilidade para entrada imediata.",
            ApplicationStatus.Rejected,
            createdAt: DateTime.UtcNow.AddDays(-12),
            durationMonths: 6,
            wantsVisit: true,
            proposedDates: new[] { DateTime.UtcNow.AddDays(-9).Date.AddHours(11), DateTime.UtcNow.AddDays(-8).Date.AddHours(15) },
            finalVisitDate: null,
            landlordProposedDate: null,
            leaseId: null,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.Landlord2Id, "Candidatura Rejeitada", "Foi selecionado outro perfil com melhor enquadramento temporal.", null, app.CreatedAt.AddDays(3))
            }));

        applications.Add(CreateApplication(
            ContractPendingApplicationId,
            Property7Id,
            IdentitySeeder.Tenant2Id,
            "Pretendo fechar o arrendamento o quanto antes e aceito os custos iniciais apresentados.",
            ApplicationStatus.ContractPendingSignature,
            createdAt: DateTime.UtcNow.AddDays(-9),
            durationMonths: 24,
            wantsVisit: false,
            proposedDates: Array.Empty<DateTime>(),
            finalVisitDate: null,
            landlordProposedDate: null,
            leaseId: Lease3Id,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.LandlordId, "Candidatura Aprovada (Contrato)", "Aprovado para arrendamento com contrato oficial.", null, app.CreatedAt.AddDays(1)),
                NewHistory(app.Id, IdentitySeeder.LandlordId, "Procedimento de Arrendamento Iniciado", "Data proposta: {placeholder}", null, app.CreatedAt.AddDays(2)),
                NewHistory(app.Id, IdentitySeeder.LandlordId, "Data de Inicio Confirmada", "Contrato gerado e a aguardar assinatura do proprietario.", null, app.CreatedAt.AddDays(3))
            }));

        applications.Add(CreateApplication(
            AwaitingPaymentApplicationId,
            Property8Id,
            IdentitySeeder.TenantId,
            "Os termos estao alinhados com o meu orcamento e posso efetuar o pagamento inicial de imediato.",
            ApplicationStatus.AwaitingPayment,
            createdAt: DateTime.UtcNow.AddDays(-7),
            durationMonths: 12,
            wantsVisit: false,
            proposedDates: Array.Empty<DateTime>(),
            finalVisitDate: null,
            landlordProposedDate: null,
            leaseId: Lease4Id,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.Landlord2Id, "Candidatura Aprovada (Contrato)", "Termos informais aprovados.", null, app.CreatedAt.AddDays(1)),
                NewHistory(app.Id, IdentitySeeder.TenantId, "Aceitacao de Termos", "O inquilino aceitou os termos e falta liquidar o pagamento inicial.", null, app.CreatedAt.AddDays(3))
            }));

        applications.Add(CreateApplication(
            LeaseStartDateProposedApplicationId,
            Property9Id,
            IdentitySeeder.Tenant2Id,
            "Gostava de alinhar a data de entrada antes de avançar para assinatura.",
            ApplicationStatus.LeaseStartDateProposed,
            createdAt: DateTime.UtcNow.AddDays(-6),
            durationMonths: 12,
            wantsVisit: false,
            proposedDates: Array.Empty<DateTime>(),
            finalVisitDate: null,
            landlordProposedDate: null,
            leaseId: Lease5Id,
            historyFactory: app => new[]
            {
                NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                NewHistory(app.Id, IdentitySeeder.Landlord2Id, "Candidatura Aprovada (Contrato)", "Aguardamos alinhamento da data de inicio.", null, app.CreatedAt.AddDays(1)),
                NewHistory(app.Id, IdentitySeeder.Landlord2Id, "Procedimento de Arrendamento Iniciado", "Foi proposta uma data de inicio para o lease.", null, app.CreatedAt.AddDays(2))
            }));

        var availableProperties = properties.Where(p => p.IsPublic && !p.TenantId.HasValue).ToList();
        var additionalApplications = 0;
        var attempts = 0;
        while (additionalApplications < 24 && attempts < 200)
        {
            attempts++;

            var property = availableProperties[random.Next(availableProperties.Count)];
            var tenantId = userIds[random.Next(userIds.Count)];
            if (tenantId == property.LandlordId)
                continue;

            var candidateDurations = property.AcceptedPeriodicities.Select(p => p.DurationMonths).Distinct().ToList();
            if (candidateDurations.Count == 0)
                continue;

            var proposedDates = new[]
            {
                DateTime.UtcNow.AddDays(random.Next(2, 7)).Date.AddHours(random.Next(10, 19)),
                DateTime.UtcNow.AddDays(random.Next(8, 13)).Date.AddHours(random.Next(10, 19))
            };

            var status = random.NextDouble() switch
            {
                > 0.86 => ApplicationStatus.Rejected,
                > 0.65 => ApplicationStatus.VisitCounterProposed,
                _ => ApplicationStatus.Pending
            };

            var application = CreateApplication(
                id: Guid.NewGuid(),
                propertyId: property.Id,
                tenantId: tenantId,
                message: "Procuro um espaco cuidado e consigo enviar referencias e comprovativos rapidamente.",
                status: status,
                createdAt: DateTime.UtcNow.AddDays(-random.Next(1, 14)),
                durationMonths: candidateDurations[random.Next(candidateDurations.Count)],
                wantsVisit: true,
                proposedDates: proposedDates,
                finalVisitDate: null,
                landlordProposedDate: status == ApplicationStatus.VisitCounterProposed ? DateTime.UtcNow.AddDays(random.Next(4, 10)).Date.AddHours(18) : null,
                leaseId: null,
                historyFactory: app => status switch
                {
                    ApplicationStatus.VisitCounterProposed => new[]
                    {
                        NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                        NewHistory(app.Id, property.LandlordId, "Senhorio Contra-Propos", "Tenho disponibilidade noutro horario.", app.LandlordProposedDate?.ToString("O"), app.CreatedAt.AddDays(1))
                    },
                    ApplicationStatus.Rejected => new[]
                    {
                        NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt),
                        NewHistory(app.Id, property.LandlordId, "Candidatura Rejeitada", "O senhorio decidiu nao avancar com esta candidatura.", null, app.CreatedAt.AddDays(2))
                    },
                    _ => new[]
                    {
                        NewHistory(app.Id, app.TenantId, "Criada", app.Message, app.TenantProposedDates, app.CreatedAt)
                    }
                });

            applications.Add(application);
            additionalApplications++;
        }

        try
        {
            context.Properties.AddRange(properties);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Catalog: {properties.Count} imoveis criados (9 core + 61 dinamicos).");

            context.Applications.AddRange(applications);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Catalog: {applications.Count} candidaturas criadas com estados de teste e historico coerente.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] Catalog: Erro no Seed — {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static Property CreateProperty(
        Guid id,
        Guid landlordId,
        Guid? tenantId,
        string title,
        string description,
        decimal price,
        string propertyType,
        string typology,
        decimal area,
        int bathrooms,
        string district,
        string municipality,
        string parish,
        string street,
        string doorNumber,
        string postalCode,
        string floor,
        double latitude,
        double longitude,
        bool isPublic,
        DateTime createdAt,
        LeaseRegime leaseRegime,
        IEnumerable<int> acceptedPeriodicities,
        decimal? deposit,
        int advanceRentMonths,
        bool allowsRenewal,
        bool hasOfficialContract,
        string condoPaidBy,
        string waterPaidBy,
        string electricityPaidBy,
        string gasPaidBy,
        IEnumerable<Guid> amenityIds,
        string[] imageUrls,
        Random random,
        string? nonPermanentReason = null,
        string? matrixArticle = null,
        string? propertyFraction = null,
        string? parishConcelho = null,
        string? energyClass = null,
        string? energyCertificateNumber = null,
        string? atRegistrationNumber = null,
        string? permanentCertNumber = null,
        string? permanentCertOffice = null,
        string? usageLicenseNumber = null,
        string? usageLicenseDate = null,
        string? usageLicenseIssuer = null)
    {
        var property = new Property
        {
            Id = id,
            LandlordId = landlordId,
            TenantId = tenantId,
            Title = title,
            Description = description,
            Price = price,
            PropertyType = propertyType,
            Typology = typology,
            Area = area,
            Rooms = GetRoomCount(typology),
            Bathrooms = bathrooms,
            Floor = floor,
            District = district,
            Municipality = municipality,
            Parish = parish,
            Street = street,
            DoorNumber = doorNumber,
            PostalCode = postalCode,
            Latitude = latitude,
            Longitude = longitude,
            IsPublic = isPublic,
            IsUnderMaintenance = false,
            HasElevator = propertyType == "Apartamento" && floor != "0" && floor != "Moradia",
            HasAirConditioning = price >= 1000,
            HasGarage = propertyType != "Quarto" && price >= 900,
            AllowsPets = amenityIds.Contains(PetsAmenityId),
            IsFurnished = true,
            FurnishedDescription = propertyType == "Quarto" ? "Quarto mobilado com secretaria e roupeiro." : "Mobilado com eletrodomesticos essenciais e sofa.",
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddDays(1),
            MatrixArticle = matrixArticle ?? $"M-{id.ToString()[..6].ToUpperInvariant()}",
            PropertyFraction = propertyFraction,
            EnergyClass = energyClass ?? (price >= 1000 ? "A" : "B"),
            EnergyCertificateNumber = energyCertificateNumber ?? $"CERT-{id.ToString()[..8].ToUpperInvariant()}",
            EnergyCertificateExpiryDate = DateTime.UtcNow.AddYears(7),
            AtRegistrationNumber = atRegistrationNumber ?? $"AT-{id.ToString()[..9].ToUpperInvariant()}",
            ParishConcelho = parishConcelho ?? $"{parish} / {municipality}",
            PermanentCertNumber = permanentCertNumber ?? $"PC-{id.ToString()[..8].ToUpperInvariant()}",
            PermanentCertOffice = permanentCertOffice ?? $"Conservatoria de {municipality}",
            UsageLicenseNumber = usageLicenseNumber ?? $"LU-{random.Next(100, 999)}/{random.Next(2008, 2023)}",
            UsageLicenseDate = usageLicenseDate ?? $"{random.Next(2010, 2023)}-{random.Next(1, 12):00}-{random.Next(1, 28):00}",
            UsageLicenseIssuer = usageLicenseIssuer ?? $"Camara Municipal de {municipality}",
            Deposit = deposit,
            AdvanceRentMonths = advanceRentMonths,
            CondominiumFeesPaidBy = condoPaidBy,
            WaterPaidBy = waterPaidBy,
            ElectricityPaidBy = electricityPaidBy,
            GasPaidBy = gasPaidBy,
            HasOfficialContract = hasOfficialContract,
            LeaseRegime = leaseRegime,
            AllowsRenewal = allowsRenewal,
            NonPermanentReason = leaseRegime == LeaseRegime.NonPermanentHousing ? nonPermanentReason : null
        };

        foreach (var months in acceptedPeriodicities.Distinct().OrderBy(months => months))
        {
            property.AcceptedPeriodicities.Add(new PropertyPeriodicity
            {
                Id = Guid.NewGuid(),
                PropertyId = property.Id,
                DurationMonths = months
            });
        }

        foreach (var amenityId in amenityIds.Distinct())
        {
            property.Amenities.Add(new PropertyAmenity
            {
                PropertyId = property.Id,
                AmenityId = amenityId
            });
        }

        property.Images.Add(new PropertyImage
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Url = imageUrls[random.Next(imageUrls.Length)] + "?auto=format&fit=crop&w=1280&q=80",
            Category = "Principal",
            IsMain = true
        });

        property.Images.Add(new PropertyImage
        {
            Id = Guid.NewGuid(),
            PropertyId = property.Id,
            Url = imageUrls[random.Next(imageUrls.Length)] + "?auto=format&fit=crop&w=1280&q=80",
            Category = propertyType == "Moradia" ? "Exterior" : "Sala",
            IsMain = false
        });

        return property;
    }

    private static Application CreateApplication(
        Guid id,
        Guid propertyId,
        Guid tenantId,
        string message,
        ApplicationStatus status,
        DateTime createdAt,
        int durationMonths,
        bool wantsVisit,
        IEnumerable<DateTime> proposedDates,
        DateTime? finalVisitDate,
        DateTime? landlordProposedDate,
        Guid? leaseId,
        Func<Application, IEnumerable<ApplicationHistory>> historyFactory)
    {
        var app = new Application
        {
            Id = id,
            PropertyId = propertyId,
            TenantId = tenantId,
            Message = message,
            ShareProfile = true,
            WantsVisit = wantsVisit,
            DurationMonths = durationMonths,
            TenantProposedDates = JsonSerializer.Serialize(proposedDates.Select(date => date.ToUniversalTime().ToString("O"))),
            FinalVisitDate = finalVisitDate?.ToUniversalTime(),
            LandlordProposedDate = landlordProposedDate?.ToUniversalTime(),
            Status = status,
            CreatedAt = createdAt,
            UpdatedAt = createdAt.AddDays(1),
            LeaseId = leaseId
        };

        app.History.AddRange(historyFactory(app));
        return app;
    }

    private static ApplicationHistory NewHistory(
        Guid applicationId,
        Guid actorId,
        string action,
        string? message,
        string? eventData,
        DateTime createdAt)
    {
        return new ApplicationHistory
        {
            Id = Guid.NewGuid(),
            ApplicationId = applicationId,
            ActorId = actorId,
            Action = action,
            Message = message,
            EventData = eventData,
            CreatedAt = createdAt
        };
    }

    private static int[] GetAcceptedPeriodicitiesFor(LeaseRegime regime, Random random)
    {
        var permanentOptions = new[] { 12, 18, 24, 36 };
        var temporaryOptions = new[] { 1, 3, 6, 9 };
        var source = regime == LeaseRegime.PermanentHousing ? permanentOptions : temporaryOptions;
        var count = regime == LeaseRegime.PermanentHousing ? random.Next(1, 4) : random.Next(1, 3);
        return source.OrderBy(_ => random.Next()).Take(count).OrderBy(value => value).ToArray();
    }

    private static Guid[] GetRandomAmenitySelection(Random random)
    {
        var allAmenityIds = new[]
        {
            WifiAmenityId,
            EquippedKitchenAmenityId,
            WashingMachineAmenityId,
            AirConditioningAmenityId,
            PoolAmenityId,
            AlarmAmenityId,
            PetsAmenityId,
            SupermarketAmenityId,
            TransportAmenityId
        };

        return allAmenityIds
            .OrderBy(_ => random.Next())
            .Take(random.Next(3, 6))
            .ToArray();
    }

    private static (double latitude, double longitude) GetLocationForCity(string city, Random random)
    {
        var baseLocation = city switch
        {
            "Lisboa" => (38.7223, -9.1393),
            "Porto" => (41.1496, -8.6109),
            "Coimbra" => (40.2033, -8.4103),
            "Braga" => (41.5454, -8.4265),
            "Faro" => (37.0194, -7.9304),
            "Aveiro" => (40.6405, -8.6538),
            "Setúbal" => (38.5244, -8.8882),
            "Viseu" => (40.6610, -7.9097),
            "Évora" => (38.5714, -7.9135),
            "Leiria" => (39.7436, -8.8071),
            _ => (39.3999, -8.2245)
        };

        return (baseLocation.Item1 + (random.NextDouble() - 0.5) / 25, baseLocation.Item2 + (random.NextDouble() - 0.5) / 25);
    }

    private static int GetRoomCount(string typology)
    {
        return typology switch
        {
            "T0" => 0,
            "T1" => 1,
            "T2" => 2,
            "T3" => 3,
            "T4" => 4,
            "Quarto" => 1,
            _ => 1
        };
    }
}
