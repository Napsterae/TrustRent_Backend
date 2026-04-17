using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Api.Services;

/// <summary>
/// Cross-module bridge: implements ICatalogAccessService so the Leasing module
/// can read and update Application/Property data in the Catalog module.
/// </summary>
public class CatalogAccessService : ICatalogAccessService
{
    private readonly CatalogDbContext _catalogDb;

    public CatalogAccessService(CatalogDbContext catalogDb)
    {
        _catalogDb = catalogDb;
    }

    public async Task<ApplicationContext?> GetApplicationContextAsync(Guid applicationId)
    {
        return await _catalogDb.Applications
            .Include(a => a.Property)
            .Where(a => a.Id == applicationId)
            .Select(a => new ApplicationContext
            {
                Id = a.Id,
                PropertyId = a.PropertyId,
                TenantId = a.TenantId,
                DurationMonths = a.DurationMonths,
                Status = (int)a.Status,
                LandlordId = a.Property!.LandlordId,
                Price = a.Property.Price,
                Deposit = a.Property.Deposit,
                AdvanceRentMonths = a.Property.AdvanceRentMonths,
                LeaseRegime = a.Property.LeaseRegime.HasValue ? a.Property.LeaseRegime.Value.ToString() : null,
                HasOfficialContract = a.Property.HasOfficialContract,
                AllowsRenewal = a.Property.AllowsRenewal,
                CondominiumFeesPaidBy = a.Property.CondominiumFeesPaidBy,
                WaterPaidBy = a.Property.WaterPaidBy,
                ElectricityPaidBy = a.Property.ElectricityPaidBy,
                GasPaidBy = a.Property.GasPaidBy,
                Street = a.Property.Street,
                DoorNumber = a.Property.DoorNumber,
                PostalCode = a.Property.PostalCode,
                Parish = a.Property.Parish,
                Municipality = a.Property.Municipality,
                District = a.Property.District,
                Typology = a.Property.Typology,
                MatrixArticle = a.Property.MatrixArticle,
                PropertyFraction = a.Property.PropertyFraction,
                UsageLicenseNumber = a.Property.UsageLicenseNumber,
                UsageLicenseDate = a.Property.UsageLicenseDate,
                UsageLicenseIssuer = a.Property.UsageLicenseIssuer,
                EnergyCertificateNumber = a.Property.EnergyCertificateNumber,
                EnergyClass = a.Property.EnergyClass,
                PropertyTitle = a.Property.Title
            })
            .FirstOrDefaultAsync();
    }

    public async Task<PropertyContext?> GetPropertyContextAsync(Guid propertyId)
    {
        return await _catalogDb.Properties
            .Where(p => p.Id == propertyId)
            .Select(p => new PropertyContext
            {
                Id = p.Id,
                LandlordId = p.LandlordId,
                Title = p.Title,
                Price = p.Price,
                Deposit = p.Deposit,
                AdvanceRentMonths = p.AdvanceRentMonths
            })
            .FirstOrDefaultAsync();
    }

    public async Task UpdateApplicationStatusAsync(Guid applicationId, int newStatus, Guid actorId, string action, string? message = null)
    {
        var application = await _catalogDb.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == applicationId);

        if (application == null) return;

        application.Status = (ApplicationStatus)newStatus;
        application.UpdatedAt = DateTime.UtcNow;
        application.History.Add(new ApplicationHistory
        {
            ApplicationId = applicationId,
            ActorId = actorId,
            Action = action,
            Message = message
        });

        await _catalogDb.SaveChangesAsync();
    }

    public async Task SetPropertyTenantAsync(Guid propertyId, Guid tenantId)
    {
        var property = await _catalogDb.Properties.FindAsync(propertyId);
        if (property == null) return;

        property.TenantId = tenantId;
        property.IsPublic = false;
        property.UpdatedAt = DateTime.UtcNow;

        await _catalogDb.SaveChangesAsync();
    }

    public async Task RejectOtherApplicationsAsync(Guid propertyId, Guid excludeApplicationId, INotificationService notificationService)
    {
        var otherApplications = await _catalogDb.Applications
            .Where(a => a.PropertyId == propertyId
                     && a.Id != excludeApplicationId
                     && a.Status != ApplicationStatus.Rejected
                     && a.Status != ApplicationStatus.LeaseActive)
            .ToListAsync();

        foreach (var other in otherApplications)
        {
            other.Status = ApplicationStatus.Rejected;
            other.UpdatedAt = DateTime.UtcNow;
            _catalogDb.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = other.Id,
                ActorId = Guid.Empty,
                Action = "Candidatura Rejeitada Automaticamente",
                Message = "O imóvel foi arrendado a outro candidato."
            });
            await notificationService.SendNotificationAsync(other.TenantId, "application",
                "A tua candidatura foi encerrada — o imóvel foi arrendado.", other.Id);
        }

        await _catalogDb.SaveChangesAsync();
    }
}
