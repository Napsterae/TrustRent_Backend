using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Identity.Seeds;

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

        var adjectives = new[] { "Acolhedor", "Moderno", "Espaçoso", "Elegante", "Rústico", "Luminoso", "Central", "Luxuoso" };
        var imageUrls = new[] {
            "https://images.unsplash.com/photo-1522708323590-d24dbb6b0267",
            "https://images.unsplash.com/photo-1502672260266-1c1ef2d93688",
            "https://images.unsplash.com/photo-1560448204-e02f11c3d0e2",
            "https://images.unsplash.com/photo-1484154218962-a197022b5858",
            "https://images.unsplash.com/photo-1493809842364-78817add7ffb",
            "https://images.unsplash.com/photo-1502005229762-cf1b2da7c5d6",
            "https://images.unsplash.com/photo-1527359443443-84a18accb60d"
        };

        // 1. ADICIONAR AS 6 PROPRIEDADES CORE (Com IDs fixos)
        properties.Add(new Property { Id = Property1Id, LandlordId = IdentitySeeder.LandlordId, Title = "T2 Renovado no Chiado com Vista Rio", Street = "Rua Garrett", DoorNumber = "10", PostalCode = "1200-204", Description = "Apartamento de luxo no coração de Lisboa.", Price = 1200, PropertyType = "Apartamento", Typology = "T2", Area = 85, Rooms = 2, Bathrooms = 1, District = "Lisboa", Municipality = "Lisboa", IsPublic = true, IsUnderMaintenance = false });
        properties.Add(new Property { Id = Property2Id, LandlordId = IdentitySeeder.LandlordId, Title = "T1 Moderno em Arroios", Street = "Rua Almirante Reis", DoorNumber = "45", PostalCode = "1000-002", Description = "Estúdio ideal para nómadas digitais.", Price = 850, PropertyType = "Apartamento", Typology = "T1", Area = 50, Rooms = 1, Bathrooms = 1, District = "Lisboa", Municipality = "Lisboa", IsPublic = true, IsUnderMaintenance = false });
        properties.Add(new Property { Id = Property3Id, LandlordId = IdentitySeeder.Landlord2Id, Title = "T3 Familiar em Cascais com Jardim", Street = "Avenida Marginal", DoorNumber = "105", PostalCode = "2750-001", Description = "Moradia espaçosa perto da praia.", Price = 1800, PropertyType = "Moradia", Typology = "T3", Area = 150, Rooms = 3, Bathrooms = 2, District = "Lisboa", Municipality = "Cascais", IsPublic = true, IsUnderMaintenance = false });
        properties.Add(new Property { Id = Property4Id, LandlordId = IdentitySeeder.Landlord2Id, Title = "Estúdio Loft no Porto - Ribeira", Street = "Rua das Flores", DoorNumber = "20", PostalCode = "4050-262", Description = "Design industrial e vista Douro.", Price = 950, PropertyType = "Apartamento", Typology = "T0", Area = 45, Rooms = 0, Bathrooms = 1, District = "Porto", Municipality = "Porto", IsPublic = true, IsUnderMaintenance = false });
        
        // 🏠 Core Rented (Já ocupadas)
        properties.Add(new Property { Id = Property5Id, LandlordId = IdentitySeeder.LandlordId, TenantId = IdentitySeeder.TenantId, Title = "Apartamento Luxo nas Torres do Colombo", Street = "Avenida Lusíada", DoorNumber = "1", PostalCode = "1500-392", Description = "Propriedade ocupada pela Ana Ferreira.", Price = 2500, PropertyType = "Apartamento", Typology = "T3", Area = 160, District = "Lisboa", Municipality = "Lisboa", IsPublic = false });
        properties.Add(new Property { Id = Property6Id, LandlordId = IdentitySeeder.Landlord2Id, TenantId = IdentitySeeder.Tenant2Id, Title = "Moradia T2 em Tavira com Piscina", Street = "Rua Direita", DoorNumber = "22", PostalCode = "8800-001", Description = "Propriedade ocupada pelo Miguel Costa.", Price = 1100, PropertyType = "Moradia", Typology = "T2", Area = 90, District = "Faro", Municipality = "Tavira", IsPublic = false });

        // 2. GERAR MAIS 64 PROPRIEDADES (Total 70)
        var cities = new[] { "Lisboa", "Porto", "Coimbra", "Braga", "Faro", "Aveiro", "Setúbal", "Viseu", "Évora", "Leiria" };
        var types = new[] { "Apartamento", "Moradia", "Quarto" };
        var typologies = new[] { "T0", "T1", "T2", "T3", "T4" };

        for (int i = 0; i < 64; i++)
        {
            var city = cities[random.Next(cities.Length)];
            var type = types[random.Next(types.Length)];
            var typo = typologies[random.Next(typologies.Length)];
            var adj = adjectives[random.Next(adjectives.Length)];
            var price = random.Next(4, 25) * 100;
            
            var p = new Property
            {
                Id = Guid.NewGuid(),
                LandlordId = userIds[random.Next(userIds.Count)],
                Title = $"{typo} {adj} em {city}",
                Description = $"Excelente {type.ToLower()} {adj.ToLower()} situado em zona calma de {city}. Perto de serviços e transportes.",
                Price = price,
                PropertyType = type,
                Typology = typo,
                Area = random.Next(40, 200),
                Rooms = random.Next(0, 5),
                Bathrooms = random.Next(1, 3),
                District = city,
                Municipality = city,
                Street = $"Rua {adj} de {city}",
                DoorNumber = random.Next(1, 200).ToString(),
                PostalCode = $"{random.Next(1000, 9000)}-{random.Next(100, 999)}",
                IsPublic = true,
                IsUnderMaintenance = false,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(1, 60))
            };

            properties.Add(p);
        }

        // Adicionar imagem obrigatória aos imóveis que não têm.
        foreach(var p in properties) 
        {
            if(p.Images.Count == 0)
            {
                p.Images.Add(new PropertyImage
                {
                    Id = Guid.NewGuid(),
                    PropertyId = p.Id,
                    Url = imageUrls[random.Next(imageUrls.Length)] + "?auto=format&fit=crop&w=800&q=80",
                    Category = "Principal",
                    IsMain = true
                });
            }
        }

        // 3. MARCAR MAIS 8 PROPRIEDADES COMO ALUGADAS (Total 10 rented)
        // Escolhemos 8 das geradas (que não as core) e atribuímos um TenantId aleatório
        var generatedProperties = properties.Skip(6).ToList();
        for (int i = 0; i < 8; i++)
        {
            var p = generatedProperties[i];
            p.TenantId = userIds[random.Next(userIds.Count)];
            p.IsPublic = false; // Casas ocupadas geralmente não estão públicas
        }

        // 4. GERAR CANDIDATURAS
        var applications = new List<Application>();
        
        // Candidaturas Aceites para as casas já alugadas (histórico)
        foreach (var p in properties.Where(p => p.TenantId.HasValue))
        {
            var app = new Application
            {
                Id = Guid.NewGuid(),
                PropertyId = p.Id,
                TenantId = p.TenantId.Value,
                Message = "Gostaria de alugar este imóvel.",
                Status = ApplicationStatus.Accepted,
                CreatedAt = p.CreatedAt.AddDays(5)
            };
            app.History.Add(new ApplicationHistory { Id = Guid.NewGuid(), ApplicationId = app.Id, ActorId = p.LandlordId, Action = "Candidatura Aceite", CreatedAt = app.CreatedAt.AddDays(1) });
            applications.Add(app);
        }

        // Gerar mais 30 candidaturas pendentes em casas variadas para teste de listas
        var availableProperties = properties.Where(p => !p.TenantId.HasValue).ToList();
        for (int i = 0; i < 30; i++)
        {
            var p = availableProperties[random.Next(availableProperties.Count)];
            var tId = userIds[random.Next(userIds.Count)];
            
            // Evitar que o landlord se candidate a si próprio
            if (tId == p.LandlordId) continue;

            var proposedDates = new List<string> { 
                DateTime.UtcNow.AddDays(random.Next(2, 5)).ToString("yyyy-MM-dd") + "T10:00:00Z",
                DateTime.UtcNow.AddDays(random.Next(6, 10)).ToString("yyyy-MM-dd") + "T16:00:00Z"
            };

            var app = new Application
            {
                Id = Guid.NewGuid(),
                PropertyId = p.Id,
                TenantId = tId,
                Message = "Tenho muito interesse em visitar este imóvel. Sou uma pessoa responsável.",
                WantsVisit = true,
                TenantProposedDates = JsonSerializer.Serialize(proposedDates),
                Status = random.NextDouble() > 0.7 ? ApplicationStatus.VisitCounterProposed : ApplicationStatus.Pending,
                CreatedAt = DateTime.UtcNow.AddDays(-random.Next(0, 10))
            };
            app.History.Add(new ApplicationHistory { 
                Id = Guid.NewGuid(), 
                ApplicationId = app.Id, 
                ActorId = tId, 
                Action = "Criada", 
                Message = app.Message, 
                EventData = app.TenantProposedDates, 
                CreatedAt = app.CreatedAt 
            });
            applications.Add(app);
        }

        try
        {
            context.Properties.AddRange(properties);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Catalog: {properties.Count} imóveis criados (6 cores + 64 dinâmicos).");

            context.Applications.AddRange(applications);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Catalog: {applications.Count} candidaturas criadas (10 aceites + 30 pendentes/propostas).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] Catalog: Erro no Seed — {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
