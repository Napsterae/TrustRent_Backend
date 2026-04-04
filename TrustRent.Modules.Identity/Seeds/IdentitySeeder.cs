using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Seeds;

public static class IdentitySeeder
{
    // IDs fixos para os "Core Users" (sempre presentes)
    public static readonly Guid LandlordId  = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantId    = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Tenant2Id   = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Landlord2Id = Guid.Parse("44444444-4444-4444-4444-444444444444");

    // Lista estática que será preenchida durante o Seed para o Catalog saber os outros user IDs
    public static List<Guid> AllGeneratedUserIds { get; private set; } = new();

    public static async Task SeedAsync(IdentityDbContext context)
    {
        // Se já temos os core users, não voltamos a gerar para não duplicar seeder dynamic
        if (await context.Users.AnyAsync(u => u.Id == LandlordId))
        {
            AllGeneratedUserIds = await context.Users.Select(u => u.Id).ToListAsync();
            Console.WriteLine($"[SEED] Identity: Já existem {AllGeneratedUserIds.Count} utilizadores. A ignorar.");
            return;
        }

        var users = new List<User>();
        var passwordHash = BCrypt.Net.BCrypt.HashPassword("TrustRent2026!");

        // 1. Adicionar os 4 Core Users
        users.Add(new User { Id = LandlordId, Name = "Carlos Mendes", Email = "carlos.mendes@email.pt", PasswordHash = passwordHash, IsIdentityVerified = true, IsNoDebtVerified = true, TrustScore = 85, ProfilePictureUrl = "https://images.unsplash.com/photo-1560250097-0b93528c311a?auto=format&fit=crop&w=256&q=80" });
        users.Add(new User { Id = TenantId, Name = "Ana Ferreira", Email = "ana.ferreira@email.pt", PasswordHash = passwordHash, IsIdentityVerified = true, TrustScore = 65, ProfilePictureUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=256&q=80" });
        users.Add(new User { Id = Tenant2Id, Name = "Miguel Costa", Email = "miguel.costa@email.pt", PasswordHash = passwordHash, TrustScore = 50 });
        users.Add(new User { Id = Landlord2Id, Name = "Sofia Rodrigues", Email = "sofia.rodrigues@email.pt", PasswordHash = passwordHash, IsIdentityVerified = true, IsNoDebtVerified = true, TrustScore = 92, ProfilePictureUrl = "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&w=256&q=80" });

        // 2. Gerar mais 16 utilizadores dinamicamente (Total 20)
        var firstNames = new[] { "João", "Maria", "Pedro", "Cláudia", "Ricardo", "Bárbara", "Luís", "Helena", "Vitor", "Daniela", "Nuno", "Rita", "André", "Inês", "Tiago", "Filipa" };
        var lastNames = new[] { "Santos", "Lopes", "Teixeira", "Vieira", "Oliveira", "Martins", "Pereira", "Silva", "Correia", "Gomes", "Almeida", "Ribeiro", "Soares", "Cunha", "Carvalho", "Sousa" };
        
        var random = new Random(42); // Seed fixa para reprodutibilidade

        for (int i = 0; i < 16; i++)
        {
            var firstName = firstNames[i];
            var lastName = lastNames[i];
            var fullName = $"{firstName} {lastName}";
            var email = $"{firstName.ToLower()}.{lastName.ToLower()}@trustrent.pt";
            
            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = fullName,
                Email = email,
                PasswordHash = passwordHash,
                IsIdentityVerified = random.NextDouble() > 0.4,
                IsNoDebtVerified = random.NextDouble() > 0.6,
                TrustScore = random.Next(40, 95),
                CreatedAt = DateTime.UtcNow.AddMonths(-random.Next(1, 12))
            });
        }

        try
        {
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
            AllGeneratedUserIds = users.Select(u => u.Id).ToList();
            Console.WriteLine($"[SEED] Identity: {users.Count} utilizadores criados (4 cores + 16 dinâmicos).");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] Identity: Erro no Seed — {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
