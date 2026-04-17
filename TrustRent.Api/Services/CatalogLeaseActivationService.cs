using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Api.Services;

/// <summary>
/// Implementação cross-module: permite ao módulo de Leasing (pagamentos)
/// ativar um lease no módulo de Leasing e atualizar dados no Catalog após pagamento confirmado.
/// </summary>
public class CatalogLeaseActivationService : ILeaseActivationService
{
    private readonly LeasingDbContext _leasingDbContext;
    private readonly ICatalogAccessService _catalogAccess;
    private readonly INotificationService _notificationService;

    public CatalogLeaseActivationService(
        LeasingDbContext leasingDbContext,
        ICatalogAccessService catalogAccess,
        INotificationService notificationService)
    {
        _leasingDbContext = leasingDbContext;
        _catalogAccess = catalogAccess;
        _notificationService = notificationService;
    }

    public async Task ActivateLeaseAfterPaymentAsync(Guid leaseId)
    {
        var lease = await _leasingDbContext.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new InvalidOperationException($"Lease {leaseId} não encontrado.");

        if (lease.Status != LeaseStatus.AwaitingPayment)
            return; // Já está ativo ou noutro estado

        var now = DateTime.UtcNow;

        // Ativar o lease
        lease.Status = LeaseStatus.Active;
        lease.ContractSignedAt ??= now;
        lease.UpdatedAt = now;

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = Guid.Empty,
            Action = "LeaseActivatedAfterPayment",
            Message = "Arrendamento ativado após confirmação do pagamento inicial."
        });

        await _leasingDbContext.SaveChangesAsync();

        // Atualizar a candidatura no módulo Catalog
        await _catalogAccess.UpdateApplicationStatusAsync(
            lease.ApplicationId,
            (int)ApplicationStatus.LeaseActive,
            Guid.Empty,
            "Arrendamento Ativo",
            $"Pagamento confirmado. O arrendamento está ativo a partir de {lease.StartDate:dd/MM/yyyy}.");

        // Atualizar TenantId no imóvel e deslistar
        await _catalogAccess.SetPropertyTenantAsync(lease.PropertyId, lease.TenantId);

        // Rejeitar outras candidaturas ativas para o mesmo imóvel
        await _catalogAccess.RejectOtherApplicationsAsync(lease.PropertyId, lease.ApplicationId, _notificationService);

        // Notificar ambas as partes
        await _notificationService.SendNotificationAsync(lease.TenantId, "lease",
            $"Pagamento confirmado! O teu arrendamento está ativo a partir de {lease.StartDate:dd/MM/yyyy}.", lease.Id);
        await _notificationService.SendNotificationAsync(lease.LandlordId, "lease",
            $"O inquilino efetuou o pagamento inicial. O arrendamento está ativo a partir de {lease.StartDate:dd/MM/yyyy}.", lease.Id);
    }
}
