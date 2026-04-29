using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Seeds;

public static class AdminPermissionsSeeder
{
    public static async Task SeedAsync(AdminDbContext db)
    {
        // Apply migrations is responsibility of caller; this seeds data.
        var existing = await db.Permissions.Select(p => p.Code).ToListAsync();
        var existingSet = existing.ToHashSet();

        foreach (var (code, desc, category) in PermissionCodes.Catalog)
        {
            var entity = await db.Permissions.FindAsync(code);
            if (entity is null)
            {
                db.Permissions.Add(new Permission { Code = code, Description = desc, Category = category });
            }
            else if (entity.Description != desc || entity.Category != category)
            {
                entity.Description = desc;
                entity.Category = category;
            }
        }
        await db.SaveChangesAsync();

        // System roles
        await UpsertSystemRoleAsync(db, "SuperAdmin", "Acesso total a toda a plataforma.",
            PermissionCodes.Catalog.Select(c => c.Code).ToArray());

        await UpsertSystemRoleAsync(db, "Moderator", "Modera conteúdos e utilizadores.", new[]
        {
            PermissionCodes.UsersRead, PermissionCodes.UsersSuspend, PermissionCodes.UsersUnsuspend,
            PermissionCodes.PropertiesRead, PermissionCodes.PropertiesApprove, PermissionCodes.PropertiesReject, PermissionCodes.PropertiesBlock, PermissionCodes.PropertiesEdit,
            PermissionCodes.ApplicationsRead, PermissionCodes.ApplicationsChangeState, PermissionCodes.ApplicationsCancel,
            PermissionCodes.ReviewsRead, PermissionCodes.ReviewsModerate,
            PermissionCodes.TicketsMaintenanceRead, PermissionCodes.TicketsSupportRead,
            PermissionCodes.AuditRead
        });

        await UpsertSystemRoleAsync(db, "Support", "Equipa de suporte ao utilizador.", new[]
        {
            PermissionCodes.TicketsSupportRead, PermissionCodes.TicketsSupportRespond, PermissionCodes.TicketsSupportAssign, PermissionCodes.TicketsSupportClose,
            PermissionCodes.UsersRead, PermissionCodes.LeasesRead, PermissionCodes.PaymentsRead,
            PermissionCodes.ApplicationsRead, PermissionCodes.PropertiesRead
        });

        await UpsertSystemRoleAsync(db, "FinanceOps", "Operações financeiras.", new[]
        {
            PermissionCodes.PaymentsRead, PermissionCodes.PaymentsRefund, PermissionCodes.PaymentsManualCharge,
            PermissionCodes.PaymentsManualMarkPaid, PermissionCodes.PaymentsViewStripe, PermissionCodes.PaymentsManageStripeAccounts,
            PermissionCodes.LeasesRead, PermissionCodes.AuditRead
        });

        await UpsertSystemRoleAsync(db, "ContentManager", "Gestão de conteúdos e dados de referência.", new[]
        {
            PermissionCodes.ReferenceRead, PermissionCodes.ReferenceAmenitiesEdit, PermissionCodes.ReferenceLocationsEdit,
            PermissionCodes.ReferencePropertyOptionsEdit, PermissionCodes.ReferenceSalaryRangesEdit, PermissionCodes.ReferencePhoneCountriesEdit,
            PermissionCodes.CommunicationsBroadcast, PermissionCodes.CommunicationsTemplatesEdit, PermissionCodes.CommunicationsBannersEdit,
            PermissionCodes.PropertiesRead
        });
    }

    private static async Task UpsertSystemRoleAsync(AdminDbContext db, string name, string desc, string[] perms)
    {
        var role = await db.Roles.Include(r => r.Permissions).FirstOrDefaultAsync(r => r.Name == name);
        if (role is null)
        {
            role = new AdminRole
            {
                Id = Guid.NewGuid(),
                Name = name,
                Description = desc,
                IsSystem = true
            };
            db.Roles.Add(role);
            await db.SaveChangesAsync();
        }
        else if (role.Description != desc)
        {
            role.Description = desc;
            await db.SaveChangesAsync();
        }

        var validCodes = (await db.Permissions.Select(p => p.Code).ToListAsync()).ToHashSet();
        var current = role.Permissions.Select(p => p.PermissionCode).ToHashSet();
        var target = perms.Where(p => validCodes.Contains(p)).ToHashSet();

        // Add missing
        foreach (var p in target.Except(current))
            db.RolePermissions.Add(new AdminRolePermission { RoleId = role.Id, PermissionCode = p });

        // For system roles, also remove permissions that no longer apply (keeps Moderator etc. in sync with code).
        foreach (var p in current.Except(target))
        {
            var rp = await db.RolePermissions.FindAsync(role.Id, p);
            if (rp is not null) db.RolePermissions.Remove(rp);
        }
        await db.SaveChangesAsync();
    }
}
