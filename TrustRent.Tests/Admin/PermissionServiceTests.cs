using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;
using TrustRent.Modules.Admin.Services;

namespace TrustRent.Tests.Admin;

public class PermissionServiceTests
{
    private static AdminDbContext NewDb()
    {
        var opts = new DbContextOptionsBuilder<AdminDbContext>()
            .UseInMemoryDatabase($"admin-{Guid.NewGuid()}")
            .Options;
        return new AdminDbContext(opts);
    }

    private static IMemoryCache NewCache() => new MemoryCache(new MemoryCacheOptions());

    [Fact]
    public async Task SuperAdmin_Recebe_Todas_As_Permissoes_Do_Catalogo()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        db.AdminUsers.Add(new AdminUser { Id = userId, Email = "s@x.pt", Name = "Super", IsSuperAdmin = true });
        db.Permissions.Add(new Permission { Code = "audit.read", Description = "x", Category = "x" });
        db.Permissions.Add(new Permission { Code = "users.read", Description = "x", Category = "x" });
        await db.SaveChangesAsync();

        var svc = new PermissionService(db, NewCache());
        var perms = await svc.GetEffectivePermissionsAsync(userId);

        perms.Should().BeEquivalentTo(new[] { "audit.read", "users.read" });
    }

    [Fact]
    public async Task Permissoes_Sao_Resolvidas_Via_Roles_E_Aplicam_Overrides_Deny()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.AdminUsers.Add(new AdminUser { Id = userId, Email = "u@x.pt", Name = "U", IsSuperAdmin = false });
        db.Roles.Add(new AdminRole { Id = roleId, Name = "Operator" });
        db.UserRoles.Add(new AdminUserRole { AdminUserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new AdminRolePermission { RoleId = roleId, PermissionCode = "users.read" });
        db.RolePermissions.Add(new AdminRolePermission { RoleId = roleId, PermissionCode = "users.suspend" });
        // override: revoga users.suspend
        db.UserPermissions.Add(new AdminUserPermission { AdminUserId = userId, PermissionCode = "users.suspend", IsGrant = false });
        // override: concede audit.read
        db.UserPermissions.Add(new AdminUserPermission { AdminUserId = userId, PermissionCode = "audit.read", IsGrant = true });
        await db.SaveChangesAsync();

        var svc = new PermissionService(db, NewCache());

        (await svc.HasPermissionAsync(userId, "users.read")).Should().BeTrue();
        (await svc.HasPermissionAsync(userId, "users.suspend")).Should().BeFalse();
        (await svc.HasPermissionAsync(userId, "audit.read")).Should().BeTrue();
        (await svc.HasPermissionAsync(userId, "users.delete")).Should().BeFalse();
    }

    [Fact]
    public async Task Bump_Invalida_Cache_E_Reflete_Novas_Permissoes()
    {
        await using var db = NewDb();
        var userId = Guid.NewGuid();
        var roleId = Guid.NewGuid();
        db.AdminUsers.Add(new AdminUser { Id = userId, Email = "u@x.pt", Name = "U" });
        db.Roles.Add(new AdminRole { Id = roleId, Name = "R" });
        db.UserRoles.Add(new AdminUserRole { AdminUserId = userId, RoleId = roleId });
        db.RolePermissions.Add(new AdminRolePermission { RoleId = roleId, PermissionCode = "users.read" });
        await db.SaveChangesAsync();

        var svc = new PermissionService(db, NewCache());
        var first = await svc.GetEffectivePermissionsAsync(userId);
        first.Should().Contain("users.read");

        // Adicionar nova permissão e dar bump
        db.RolePermissions.Add(new AdminRolePermission { RoleId = roleId, PermissionCode = "audit.read" });
        await db.SaveChangesAsync();

        // sem bump: cache devolve antigos
        var stale = await svc.GetEffectivePermissionsAsync(userId);
        stale.Should().NotContain("audit.read");

        await svc.BumpPermissionsVersionAsync(userId);
        var fresh = await svc.GetEffectivePermissionsAsync(userId);
        fresh.Should().Contain("audit.read");
    }

    [Fact]
    public async Task User_Inexistente_Devolve_Conjunto_Vazio()
    {
        await using var db = NewDb();
        var svc = new PermissionService(db, NewCache());
        var perms = await svc.GetEffectivePermissionsAsync(Guid.NewGuid());
        perms.Should().BeEmpty();
    }
}
