using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;
using TrustRent.Modules.Admin.Services;

namespace TrustRent.Tests.Admin;

public class AdminAuthServiceTests
{
    private static AdminDbContext NewDb() =>
        new(new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase($"auth-{Guid.NewGuid()}").Options);

    private static IConfiguration NewConfig() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["AdminJwtSettings:SecretKey"] = "super-segredo-de-testes-com-mais-de-32-bytes-1234",
            ["AdminJwtSettings:Issuer"] = "trustrent-admin",
            ["AdminJwtSettings:Audience"] = "trustrent-admin",
            ["AdminJwtSettings:ExpiryHours"] = "1"
        }).Build();

    private static AdminAuthService NewService(AdminDbContext db) =>
        new(db, NewConfig(), new PermissionService(db, new MemoryCache(new MemoryCacheOptions())));

    private static AdminUser SeedAdmin(AdminDbContext db, string password, bool mustChange = false, bool mfa = false)
    {
        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = "admin@trustrent.local",
            Name = "Admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true,
            IsSuperAdmin = false,
            MustChangePassword = mustChange,
            MfaEnabled = mfa
        };
        db.AdminUsers.Add(admin);
        db.SaveChanges();
        return admin;
    }

    [Fact]
    public async Task Login_Com_Credenciais_Validas_Devolve_Token_E_Permissoes()
    {
        await using var db = NewDb();
        var admin = SeedAdmin(db, "ChangeMeNow!2026");
        var svc = NewService(db);

        var result = await svc.LoginAsync(admin.Email, "ChangeMeNow!2026", null, "127.0.0.1", "test-ua");

        result.Jwt.Should().NotBeNullOrWhiteSpace();
        result.MfaRequired.Should().BeFalse();
        result.AdminUser.Id.Should().Be(admin.Id);
    }

    [Fact]
    public async Task Login_Com_Password_Errada_Lanca_Excepcao_E_Incrementa_FailedAttempts()
    {
        await using var db = NewDb();
        var admin = SeedAdmin(db, "boa-password-123");
        var svc = NewService(db);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
            svc.LoginAsync(admin.Email, "errada", null, null, null));

        var reload = await db.AdminUsers.FindAsync(admin.Id);
        reload!.FailedAttempts.Should().Be(1);
    }

    [Fact]
    public async Task Login_Bloqueia_Apos_Cinco_Tentativas_Falhadas()
    {
        await using var db = NewDb();
        var admin = SeedAdmin(db, "boa-password-123");
        var svc = NewService(db);

        for (var i = 0; i < 5; i++)
        {
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                svc.LoginAsync(admin.Email, "errada", null, null, null));
        }

        var reload = await db.AdminUsers.FindAsync(admin.Id);
        reload!.LockedUntil.Should().NotBeNull();
        reload.LockedUntil!.Value.Should().BeAfter(DateTime.UtcNow);
    }

    [Fact]
    public async Task ChangePassword_Atualiza_Hash_Limpa_MustChange_E_Roda_SecurityStamp()
    {
        await using var db = NewDb();
        var admin = SeedAdmin(db, "ChangeMeNow!2026", mustChange: true);
        var svc = NewService(db);
        var stampBefore = admin.SecurityStamp;

        await svc.ChangePasswordAsync(admin.Id, "ChangeMeNow!2026", "NovaPasswordForte!1");

        var reload = await db.AdminUsers.FindAsync(admin.Id);
        reload!.MustChangePassword.Should().BeFalse();
        reload.SecurityStamp.Should().NotBe(stampBefore);
        BCrypt.Net.BCrypt.Verify("NovaPasswordForte!1", reload.PasswordHash).Should().BeTrue();
    }

    [Fact]
    public async Task ChangePassword_Rejeita_Password_Curta()
    {
        await using var db = NewDb();
        var admin = SeedAdmin(db, "ChangeMeNow!2026");
        var svc = NewService(db);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            svc.ChangePasswordAsync(admin.Id, "ChangeMeNow!2026", "curta"));
    }
}
