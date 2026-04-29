using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Seeds;

public static class AdminBootstrapSeeder
{
    public const string PasswordEnvVar = "TRUSTRENT_BOOTSTRAP_ADMIN_PASSWORD";

    public static async Task SeedAsync(AdminDbContext db, IConfiguration config, IHostEnvironment env, ILogger logger)
    {
        var hasAdmin = await db.AdminUsers.AnyAsync(x => x.DeletedAt == null);
        if (hasAdmin) return;

        var email = config["AdminBootstrap:Email"] ?? "admin@trustrent.local";
        var name = config["AdminBootstrap:Name"] ?? "Bootstrap Admin";
        var configuredPassword = config["AdminBootstrap:Password"];
        var envPassword = Environment.GetEnvironmentVariable(PasswordEnvVar);
        var password = !string.IsNullOrWhiteSpace(envPassword) ? envPassword : configuredPassword;

        if (string.IsNullOrWhiteSpace(password))
        {
            if (env.IsDevelopment())
            {
                password = "ChangeMeNow!2026";
                logger.LogWarning("[AdminBootstrap] Nenhuma password configurada. Em desenvolvimento usa-se '{Password}'. Defina {EnvVar} para produção.", password, PasswordEnvVar);
            }
            else
            {
                throw new InvalidOperationException(
                    $"Bootstrap admin password missing. Set {PasswordEnvVar} or AdminBootstrap:Password in configuration.");
            }
        }

        var admin = new AdminUser
        {
            Id = Guid.NewGuid(),
            Email = email.Trim().ToLowerInvariant(),
            Name = name,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            IsActive = true,
            IsSuperAdmin = true,
            MustChangePassword = true,
            PasswordChangedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow
        };
        db.AdminUsers.Add(admin);

        // Attach SuperAdmin role too (for visualization).
        var superRole = await db.Roles.FirstOrDefaultAsync(r => r.Name == "SuperAdmin");
        if (superRole is not null)
            db.UserRoles.Add(new AdminUserRole { AdminUserId = admin.Id, RoleId = superRole.Id });

        await db.SaveChangesAsync();
        logger.LogInformation("[AdminBootstrap] Super-admin inicial criado: {Email}", admin.Email);
    }
}
