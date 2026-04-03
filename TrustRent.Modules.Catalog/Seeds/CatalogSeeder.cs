using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Seeds;

public static class CatalogSeeder
{
    // IDs de utilizadores (do IdentitySeeder)
    private static readonly Guid LandlordId  = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId    = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant2Id   = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Landlord2Id = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // IDs fixos para propriedades — Carlos
    public static readonly Guid Property1Id = Guid.Parse("aaaa1111-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property2Id = Guid.Parse("aaaa2222-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // IDs fixos para propriedades — Sofia
    public static readonly Guid Property3Id = Guid.Parse("aaaa3333-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    public static readonly Guid Property4Id = Guid.Parse("aaaa4444-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    // IDs fixos para candidaturas
    public static readonly Guid Application1Id = Guid.Parse("bbbb1111-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid Application2Id = Guid.Parse("bbbb2222-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static async Task SeedAsync(CatalogDbContext context)
    {
        if (await context.Properties.AnyAsync(p => p.Id == Property1Id))
        {
            Console.WriteLine("[SEED] Catalog: Dados de teste já existem, a ignorar.");
            return;
        }

        // ═══════════════════════════════════════════
        //  PROPRIEDADES — Carlos Mendes (Landlord 1)
        // ═══════════════════════════════════════════
        var properties = new List<Property>
        {
            new()
            {
                Id = Property1Id,
                LandlordId = LandlordId,
                Title = "T2 Renovado no Chiado com Vista Rio",
                Description = "Apartamento completamente renovado em 2024, com acabamentos de alta qualidade. Cozinha equipada, 2 quartos com roupeiros embutidos, sala ampla com vista para o Tejo. Edifício com elevador.",
                Price = 1200, PropertyType = "Apartamento", Typology = "T2",
                Area = 85, Rooms = 2, Bathrooms = 1, Floor = "3",
                HasElevator = true, HasAirConditioning = true, HasGarage = false,
                AllowsPets = true, IsFurnished = true,
                FurnishedDescription = "Totalmente mobilado com mobiliário moderno.",
                District = "Lisboa", Municipality = "Lisboa", Parish = "Santa Maria Maior",
                DoorNumber = "42", Street = "Rua do Alecrim", PostalCode = "1200-018",
                Latitude = 38.7095, Longitude = -9.1420,
                IsPublic = true, IsUnderMaintenance = false,
                EnergyClass = "B", EnergyCertificateNumber = "SCE-123456789",
                EnergyCertificateExpiryDate = new DateTime(2030, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                AtRegistrationNumber = "AT-LIS-2024-001", MatrixArticle = "U-1234", PropertyFraction = "A",
                Images = new List<PropertyImage>
                {
                    new() { Id = Guid.NewGuid(), PropertyId = Property1Id, Url = "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688?auto=format&fit=crop&w=800&q=80", Category = "Sala", IsMain = true },
                    new() { Id = Guid.NewGuid(), PropertyId = Property1Id, Url = "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2?auto=format&fit=crop&w=800&q=80", Category = "Quarto", IsMain = false },
                    new() { Id = Guid.NewGuid(), PropertyId = Property1Id, Url = "https://images.unsplash.com/photo-1556909114-f6e7ad7d3136?auto=format&fit=crop&w=800&q=80", Category = "Cozinha", IsMain = false },
                }
            },
            new()
            {
                Id = Property2Id,
                LandlordId = LandlordId,
                Title = "T1 Moderno em Arroios com Terraço",
                Description = "Estúdio moderno com terraço privativo de 15m², ideal para jovens profissionais. Zona muito central com metro a 2 minutos.",
                Price = 850, PropertyType = "Apartamento", Typology = "T1",
                Area = 55, Rooms = 1, Bathrooms = 1, Floor = "5",
                HasElevator = true, HasAirConditioning = false, HasGarage = false,
                AllowsPets = false, IsFurnished = false,
                District = "Lisboa", Municipality = "Lisboa", Parish = "Arroios",
                DoorNumber = "7B", Street = "Rua Morais Soares", PostalCode = "1900-341",
                Latitude = 38.7320, Longitude = -9.1330,
                IsPublic = true, IsUnderMaintenance = false, EnergyClass = "C",
                Images = new List<PropertyImage>
                {
                    new() { Id = Guid.NewGuid(), PropertyId = Property2Id, Url = "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267?auto=format&fit=crop&w=800&q=80", Category = "Sala", IsMain = true },
                }
            },

            // ═══════════════════════════════════════════
            //  PROPRIEDADES — Sofia Rodrigues (Landlord 2)
            // ═══════════════════════════════════════════
            new()
            {
                Id = Property3Id,
                LandlordId = Landlord2Id,
                Title = "T3 Familiar em Cascais com Jardim",
                Description = "Moradia geminada com jardim privativo, ideal para famílias. 3 quartos (1 suite), sala espaçosa com lareira, cozinha totalmente equipada, garagem para 2 carros. Zona residencial tranquila a 10 minutos da praia.",
                Price = 1800, PropertyType = "Moradia", Typology = "T3",
                Area = 140, Rooms = 3, Bathrooms = 2, Floor = "R/C + 1",
                HasElevator = false, HasAirConditioning = true, HasGarage = true,
                AllowsPets = true, IsFurnished = true,
                FurnishedDescription = "Mobilado com peças de design escandinavo. Inclui máquina de lavar loiça, roupa e forno.",
                District = "Lisboa", Municipality = "Cascais", Parish = "Cascais e Estoril",
                DoorNumber = "15", Street = "Rua das Flores", PostalCode = "2750-342",
                Latitude = 38.6979, Longitude = -9.4215,
                IsPublic = true, IsUnderMaintenance = false,
                EnergyClass = "A", EnergyCertificateNumber = "SCE-987654321",
                EnergyCertificateExpiryDate = new DateTime(2032, 6, 1, 0, 0, 0, DateTimeKind.Utc),
                AtRegistrationNumber = "AT-CAS-2025-001", MatrixArticle = "U-5678", PropertyFraction = "UNICA",
                Images = new List<PropertyImage>
                {
                    new() { Id = Guid.NewGuid(), PropertyId = Property3Id, Url = "https://images.unsplash.com/photo-1600596542815-ffad4c1539a9?auto=format&fit=crop&w=800&q=80", Category = "Exterior", IsMain = true },
                    new() { Id = Guid.NewGuid(), PropertyId = Property3Id, Url = "https://images.unsplash.com/photo-1600607687939-ce8a6c25118c?auto=format&fit=crop&w=800&q=80", Category = "Sala", IsMain = false },
                    new() { Id = Guid.NewGuid(), PropertyId = Property3Id, Url = "https://images.unsplash.com/photo-1600566753190-17f0baa2a6c3?auto=format&fit=crop&w=800&q=80", Category = "Cozinha", IsMain = false },
                    new() { Id = Guid.NewGuid(), PropertyId = Property3Id, Url = "https://images.unsplash.com/photo-1600585154340-be6161a56a0c?auto=format&fit=crop&w=800&q=80", Category = "Jardim", IsMain = false },
                }
            },
            new()
            {
                Id = Property4Id,
                LandlordId = Landlord2Id,
                Title = "Estúdio Loft no Porto - Ribeira",
                Description = "Loft industrial reconvertido em pleno centro histórico do Porto, com vista para o rio Douro. Tetos altos de 4m, vigas de ferro originais, chão em madeira maciça. Espaço aberto com cozinha americana. A 5 minutos da Ponte D. Luís.",
                Price = 950, PropertyType = "Apartamento", Typology = "T0",
                Area = 45, Rooms = 0, Bathrooms = 1, Floor = "2",
                HasElevator = false, HasAirConditioning = true, HasGarage = false,
                AllowsPets = false, IsFurnished = true,
                FurnishedDescription = "Decoração industrial-chic com mobiliário vintage curado. Inclui smart TV 55\" e sistema de som.",
                District = "Porto", Municipality = "Porto", Parish = "Cedofeita",
                DoorNumber = "88", Street = "Rua de Miragaia", PostalCode = "4050-387",
                Latitude = 41.1413, Longitude = -8.6239,
                IsPublic = true, IsUnderMaintenance = false,
                EnergyClass = "B-", EnergyCertificateNumber = "SCE-456789123",
                EnergyCertificateExpiryDate = new DateTime(2031, 9, 1, 0, 0, 0, DateTimeKind.Utc),
                AtRegistrationNumber = "AT-PRT-2025-042",
                Images = new List<PropertyImage>
                {
                    new() { Id = Guid.NewGuid(), PropertyId = Property4Id, Url = "https://images.unsplash.com/photo-1536376072261-38c75010e6c9?auto=format&fit=crop&w=800&q=80", Category = "Sala", IsMain = true },
                    new() { Id = Guid.NewGuid(), PropertyId = Property4Id, Url = "https://images.unsplash.com/photo-1600210492493-0946911123ea?auto=format&fit=crop&w=800&q=80", Category = "Quarto", IsMain = false },
                }
            }
        };

        // ═══ CANDIDATURAS ═══
        var proposedDates = JsonSerializer.Serialize(new List<string> { "2026-05-10", "2026-05-12", "2026-05-15" });

        var applications = new List<Application>
        {
            new()
            {
                Id = Application1Id, PropertyId = Property1Id, TenantId = TenantId,
                Message = "Olá Carlos! Sou a Ana, tenho 28 anos e trabalho como designer no centro de Lisboa. Estou à procura de um T2 perto do trabalho e o seu imóvel parece perfeito. Sou uma inquilina responsável e silenciosa. Gostaria muito de agendar uma visita!",
                ShareProfile = true, WantsVisit = true, TenantProposedDates = proposedDates,
                Status = ApplicationStatus.Pending,
                History = new List<ApplicationHistory>
                {
                    new() { Id = Guid.NewGuid(), ApplicationId = Application1Id, ActorId = TenantId, Action = "Criada", Message = "Candidatura submetida pela inquilina.", EventData = proposedDates, CreatedAt = DateTime.UtcNow.AddDays(-2) }
                }
            },
            new()
            {
                Id = Application2Id, PropertyId = Property2Id, TenantId = Tenant2Id,
                Message = "Bom dia! Chamo-me Miguel e sou estudante de mestrado na NOVA. Procuro um T1 acessível e perto do metro. Tenho referências do meu senhorio atual se necessário.",
                ShareProfile = true, WantsVisit = true,
                TenantProposedDates = JsonSerializer.Serialize(new List<string> { "2026-05-08", "2026-05-09" }),
                LandlordProposedDate = new DateTime(2026, 5, 11, 15, 0, 0, DateTimeKind.Utc),
                Status = ApplicationStatus.VisitCounterProposed,
                History = new List<ApplicationHistory>
                {
                    new() { Id = Guid.NewGuid(), ApplicationId = Application2Id, ActorId = Tenant2Id, Action = "Criada", Message = "Candidatura submetida pelo inquilino.", EventData = JsonSerializer.Serialize(new List<string> { "2026-05-08", "2026-05-09" }), CreatedAt = DateTime.UtcNow.AddDays(-3) },
                    new() { Id = Guid.NewGuid(), ApplicationId = Application2Id, ActorId = LandlordId, Action = "Senhorio Contra-Propôs", EventData = new DateTime(2026, 5, 11, 15, 0, 0, DateTimeKind.Utc).ToString("O"), CreatedAt = DateTime.UtcNow.AddDays(-1) }
                }
            }
        };

        try
        {
            context.Properties.AddRange(properties);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Catalog: {properties.Count} imóveis criados.");

            context.Applications.AddRange(applications);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Catalog: {applications.Count} candidaturas de teste criadas.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] Catalog: Seed ignorada — {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
