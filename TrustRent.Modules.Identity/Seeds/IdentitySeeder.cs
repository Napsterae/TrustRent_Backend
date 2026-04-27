using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Security;

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

        // 1. Adicionar os 4 Core Users (todos completos)
        users.Add(new User
        {
            Id = LandlordId, Name = "Carlos Mendes", Email = "carlos.mendes@email.pt", PasswordHash = passwordHash,
            Nif = "123456789", CitizenCardNumber = "12345678", Address = "Rua Augusta 45, 2º Esq", PostalCode = "1100-048",
            PhoneCountryCode = "PT", PhoneNumber = "+351912345678",
            ProfilePictureUrl = "https://images.unsplash.com/photo-1560250097-0b93528c311a?auto=format&fit=crop&w=256&q=80",
            IsIdentityVerified = true, IdentityExpiryDate = DateTime.UtcNow.AddYears(3),
            IsNoDebtVerified = true, NoDebtExpiryDate = DateTime.UtcNow.AddMonths(6),
            TrustScore = 85, CreatedAt = DateTime.UtcNow.AddMonths(-8)
        });
        users.Add(new User
        {
            Id = TenantId, Name = "Ana Ferreira", Email = "ana.ferreira@email.pt", PasswordHash = passwordHash,
            Nif = "987654321", CitizenCardNumber = "87654321", Address = "Avenida da Liberdade 120, 5º Dto", PostalCode = "1250-146",
            PhoneCountryCode = "PT", PhoneNumber = "+351923456789",
            ProfilePictureUrl = "https://images.unsplash.com/photo-1494790108377-be9c29b29330?auto=format&fit=crop&w=256&q=80",
            IsIdentityVerified = true, IdentityExpiryDate = DateTime.UtcNow.AddYears(2),
            IsNoDebtVerified = false,
            TrustScore = 65, CreatedAt = DateTime.UtcNow.AddMonths(-6)
        });
        users.Add(new User
        {
            Id = Tenant2Id, Name = "Miguel Costa", Email = "miguel.costa@email.pt", PasswordHash = passwordHash,
            Nif = "456789123", CitizenCardNumber = "45678912", Address = "Rua de Santa Catarina 200, 3º", PostalCode = "4000-442",
            PhoneCountryCode = "PT", PhoneNumber = "+351934567890",
            ProfilePictureUrl = "https://images.unsplash.com/photo-1507003211169-0a1dd7228f2d?auto=format&fit=crop&w=256&q=80",
            IsIdentityVerified = true, IdentityExpiryDate = DateTime.UtcNow.AddYears(4),
            IsNoDebtVerified = true, NoDebtExpiryDate = DateTime.UtcNow.AddMonths(3),
            TrustScore = 50, CreatedAt = DateTime.UtcNow.AddMonths(-4)
        });
        users.Add(new User
        {
            Id = Landlord2Id, Name = "Sofia Rodrigues", Email = "sofia.rodrigues@email.pt", PasswordHash = passwordHash,
            Nif = "789123456", CitizenCardNumber = "78912345", Address = "Praça do Comércio 5, 1º", PostalCode = "1100-148",
            PhoneCountryCode = "PT", PhoneNumber = "+351945678901",
            ProfilePictureUrl = "https://images.unsplash.com/photo-1573496359142-b8d87734a5a2?auto=format&fit=crop&w=256&q=80",
            IsIdentityVerified = true, IdentityExpiryDate = DateTime.UtcNow.AddYears(5),
            IsNoDebtVerified = true, NoDebtExpiryDate = DateTime.UtcNow.AddMonths(9),
            TrustScore = 92, CreatedAt = DateTime.UtcNow.AddMonths(-10)
        });

        // 2. Gerar mais 16 utilizadores dinamicamente (Total 20) — todos com campos preenchidos
        var dynamicUsers = new[]
        {
            ("João",    "Santos",   "PT", "+351956781001", "111222333", "11122233", "Rua do Carmo 15, 1º Dto",           "1200-093", "https://images.unsplash.com/photo-1472099645785-5658abf4ff4e?auto=format&fit=crop&w=256&q=80"),
            ("Maria",   "Lopes",    "PT", "+351956781002", "222333444", "22233344", "Travessa do Fala-Só 7",             "1200-155", "https://images.unsplash.com/photo-1438761681033-6461ffad8d80?auto=format&fit=crop&w=256&q=80"),
            ("Pedro",   "Teixeira", "PT", "+351956781003", "333444555", "33344455", "Rua de Cedofeita 90, 2º Esq",      "4050-180", "https://images.unsplash.com/photo-1500648767791-00dcc994a43e?auto=format&fit=crop&w=256&q=80"),
            ("Cláudia", "Vieira",   "PT", "+351956781004", "444555666", "44455566", "Avenida dos Aliados 30",           "4000-064", "https://images.unsplash.com/photo-1544005313-94ddf0286df2?auto=format&fit=crop&w=256&q=80"),
            ("Ricardo", "Oliveira", "PT", "+351956781005", "555666777", "55566677", "Largo do Chiado 8, 3º",            "1200-108", "https://images.unsplash.com/photo-1506794778202-cad84cf45f1d?auto=format&fit=crop&w=256&q=80"),
            ("Bárbara", "Martins",  "PT", "+351956781006", "666777888", "66677788", "Rua de São Bento 45",              "1200-822", "https://images.unsplash.com/photo-1534528741775-53994a69daeb?auto=format&fit=crop&w=256&q=80"),
            ("Luís",    "Pereira",  "PT", "+351956781007", "777888999", "77788899", "Praça da República 12, 4º Dto",    "3000-343", "https://images.unsplash.com/photo-1519085360753-af0119f7cbe7?auto=format&fit=crop&w=256&q=80"),
            ("Helena",  "Silva",    "PT", "+351956781008", "888999000", "88899900", "Rua da Junqueira 120",             "1300-344", "https://images.unsplash.com/photo-1580489944761-15a19d654956?auto=format&fit=crop&w=256&q=80"),
            ("Vitor",   "Correia",  "PT", "+351956781009", "999000111", "99900011", "Avenida da Boavista 234, 6º",      "4100-130", "https://images.unsplash.com/photo-1504257432389-52343af06ae3?auto=format&fit=crop&w=256&q=80"),
            ("Daniela", "Gomes",    "PT", "+351956781010", "000111222", "00011122", "Rua dos Clerigos 50",              "4050-204", "https://images.unsplash.com/photo-1531746020798-e6953c6e8e04?auto=format&fit=crop&w=256&q=80"),
            ("Nuno",    "Almeida",  "PT", "+351956781011", "112233445", "11223344", "Largo da Portagem 3",              "3000-337", "https://images.unsplash.com/photo-1463453091185-61582044d556?auto=format&fit=crop&w=256&q=80"),
            ("Rita",    "Ribeiro",  "PT", "+351956781012", "223344556", "22334455", "Rua Ferreira Borges 65, 2º Esq",   "3000-180", "https://images.unsplash.com/photo-1488426862026-3ee34a7d66df?auto=format&fit=crop&w=256&q=80"),
            ("André",   "Soares",   "PT", "+351956781013", "334455667", "33445566", "Avenida Sá da Bandeira 100",       "3000-351", "https://images.unsplash.com/photo-1492562080023-ab3db95bfbce?auto=format&fit=crop&w=256&q=80"),
            ("Inês",    "Cunha",    "PT", "+351956781014", "445566778", "44556677", "Rua do Almada 342, 1º Dto",        "4050-031", "https://images.unsplash.com/photo-1517841905240-472988babdf9?auto=format&fit=crop&w=256&q=80"),
            ("Tiago",   "Carvalho", "PT", "+351956781015", "556677889", "55667788", "Praça do Giraldo 18",              "7000-508", "https://images.unsplash.com/photo-1528892952291-009c663ce843?auto=format&fit=crop&w=256&q=80"),
            ("Filipa",  "Sousa",    "PT", "+351956781016", "667788990", "66778899", "Rua 5 de Outubro 77, 3º Esq",      "8000-077", "https://images.unsplash.com/photo-1524504388940-b1c1722653e1?auto=format&fit=crop&w=256&q=80"),
        };

        var random = new Random(42);

        for (int i = 0; i < dynamicUsers.Length; i++)
        {
            var (firstName, lastName, phoneCode, phone, nif, cc, address, postal, pic) = dynamicUsers[i];
            var fullName = $"{firstName} {lastName}";
            var email = EmailHelper.NormalizeEmail($"{firstName}.{lastName}@wekaza.pt");
            var isIdVerified = random.NextDouble() > 0.3;
            var isDebtVerified = random.NextDouble() > 0.5;

            users.Add(new User
            {
                Id = Guid.NewGuid(),
                Name = fullName,
                Email = email,
                PasswordHash = passwordHash,
                Nif = nif,
                CitizenCardNumber = cc,
                Address = address,
                PostalCode = postal,
                PhoneCountryCode = phoneCode,
                PhoneNumber = phone,
                ProfilePictureUrl = pic,
                IsIdentityVerified = isIdVerified,
                IdentityExpiryDate = isIdVerified ? DateTime.UtcNow.AddYears(random.Next(1, 5)) : null,
                IsNoDebtVerified = isDebtVerified,
                NoDebtExpiryDate = isDebtVerified ? DateTime.UtcNow.AddMonths(random.Next(1, 12)) : null,
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
