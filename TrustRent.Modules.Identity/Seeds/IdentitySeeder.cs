using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Seeds;

public static class IdentitySeeder
{
    // IDs fixos para seeding — reutilizáveis nos outros módulos
    public static readonly Guid LandlordId  = Guid.Parse("11111111-1111-1111-1111-111111111111");
    public static readonly Guid TenantId    = Guid.Parse("22222222-2222-2222-2222-222222222222");
    public static readonly Guid Tenant2Id   = Guid.Parse("33333333-3333-3333-3333-333333333333");
    public static readonly Guid Landlord2Id = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Guid[] AllSeedIds = { LandlordId, TenantId, Tenant2Id, Landlord2Id };

    public static async Task SeedAsync(IdentityDbContext context)
    {
        var existingIds = await context.Users
            .Where(u => AllSeedIds.Contains(u.Id))
            .Select(u => u.Id)
            .ToListAsync();

        if (existingIds.Count == AllSeedIds.Length) return;

        // Remove parciais para re-inserir limpo
        if (existingIds.Count > 0)
        {
            var partialsToRemove = await context.Users
                .Where(u => AllSeedIds.Contains(u.Id))
                .ToListAsync();
            context.Users.RemoveRange(partialsToRemove);
            await context.SaveChangesAsync();
        }

        var users = new List<User>
        {
            new()
            {
                Id = LandlordId,
                Name = "Carlos Mendes",
                Email = "carlos.mendes@email.pt",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TrustRent2026!"),
                IsIdentityVerified = true,
                IdentityExpiryDate = new DateTime(2030, 12, 31, 0, 0, 0, DateTimeKind.Utc),
                IsNoDebtVerified = true,
                NoDebtExpiryDate = new DateTime(2027, 6, 30, 0, 0, 0, DateTimeKind.Utc),
                TrustScore = 85,
                ProfilePictureUrl = "https://images.unsplash.com/photo-1560250097-0b93528c311a?auto=format&fit=crop&w=256&q=80"
            },
            new()
            {
                Id = TenantId,
                Name = "Ana Ferreira",
                Email = "ana.ferreira@email.pt",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TrustRent2026!"),
                IsIdentityVerified = true,
                IdentityExpiryDate = new DateTime(2029, 8, 15, 0, 0, 0, DateTimeKind.Utc),
                IsNoDebtVerified = false,
                TrustScore = 65,
                ProfilePictureUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=256&q=80"
            },
            new()
            {
                Id = Tenant2Id,
                Name = "Miguel Costa",
                Email = "miguel.costa@email.pt",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TrustRent2026!"),
                IsIdentityVerified = false,
                IsNoDebtVerified = false,
                TrustScore = 50
            },
            new()
            {
                Id = Landlord2Id,
                Name = "Sofia Rodrigues",
                Email = "sofia.rodrigues@email.pt",
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("TrustRent2026!"),
                IsIdentityVerified = true,
                IdentityExpiryDate = new DateTime(2031, 3, 20, 0, 0, 0, DateTimeKind.Utc),
                IsNoDebtVerified = true,
                NoDebtExpiryDate = new DateTime(2027, 1, 15, 0, 0, 0, DateTimeKind.Utc),
                TrustScore = 92,
                ProfilePictureUrl = "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&w=256&q=80"
            }
        };

        try
        {
            context.Users.AddRange(users);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Identity: {users.Count} utilizadores de teste criados.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] Identity: Seed ignorada — {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}
