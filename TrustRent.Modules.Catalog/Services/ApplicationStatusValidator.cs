using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Catalog.Services;

public class ApplicationStatusValidator : IApplicationStatusValidator
{
    private readonly CatalogDbContext _context;

    public ApplicationStatusValidator(CatalogDbContext context)
    {
        _context = context;
    }

    public async Task<bool> IsApplicationChatLockedAsync(Guid applicationId)
    {
        var application = await _context.Applications.FirstOrDefaultAsync(a => a.Id == applicationId);
        if (application == null) return true; // Segurança: Se não encontrar, tranca.

        return application.Status == ApplicationStatus.Rejected || 
               application.Status == ApplicationStatus.Accepted;
    }

    public async Task<(Guid TenantId, Guid LandlordId, Guid? CoTenantUserId)?> GetApplicationParticipantsAsync(Guid applicationId)
    {
        var application = await _context.Applications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null || application.Property == null) return null;

        return (application.TenantId, application.Property.LandlordId, application.CoTenantUserId);
    }
}
