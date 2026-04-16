using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Api.Services;

/// <summary>
/// Implementação cross-module: permite ao módulo de Leasing (pagamentos)
/// ativar um lease no módulo Catalog após pagamento confirmado.
/// </summary>
public class CatalogLeaseActivationService : ILeaseActivationService
{
    private readonly CatalogDbContext _catalogDbContext;
    private readonly INotificationService _notificationService;

    public CatalogLeaseActivationService(
        CatalogDbContext catalogDbContext,
        INotificationService notificationService)
    {
        _catalogDbContext = catalogDbContext;
        _notificationService = notificationService;
    }

    public async Task ActivateLeaseAfterPaymentAsync(Guid leaseId)
    {
        var lease = await _catalogDbContext.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new InvalidOperationException($"Lease {leaseId} não encontrado.");

        if (lease.Status != LeaseStatus.AwaitingPayment)
            return; // Já está ativo ou noutro estado

        var application = await _catalogDbContext.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId)
            ?? throw new InvalidOperationException("Candidatura associada não encontrada.");

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

        application.Status = ApplicationStatus.LeaseActive;
        application.UpdatedAt = now;
        application.History.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = Guid.Empty,
            Action = "Arrendamento Ativo",
            Message = $"Pagamento confirmado. O arrendamento está ativo a partir de {lease.StartDate:dd/MM/yyyy}."
        });

        // Atualizar TenantId no imóvel e deslistar
        var property = await _catalogDbContext.Properties.FindAsync(lease.PropertyId);
        if (property != null)
        {
            property.TenantId = lease.TenantId;
            property.IsPublic = false;
            property.UpdatedAt = now;
        }

        // Rejeitar outras candidaturas ativas para o mesmo imóvel
        var otherApplications = await _catalogDbContext.Applications
            .Where(a => a.PropertyId == lease.PropertyId
                     && a.Id != application.Id
                     && a.Status != ApplicationStatus.Rejected
                     && a.Status != ApplicationStatus.LeaseActive)
            .ToListAsync();

        foreach (var other in otherApplications)
        {
            other.Status = ApplicationStatus.Rejected;
            other.UpdatedAt = now;
            _catalogDbContext.ApplicationHistories.Add(new ApplicationHistory
            {
                ApplicationId = other.Id,
                ActorId = Guid.Empty,
                Action = "Candidatura Rejeitada Automaticamente",
                Message = "O imóvel foi arrendado a outro candidato."
            });
            await _notificationService.SendNotificationAsync(other.TenantId, "application",
                "A tua candidatura foi encerrada — o imóvel foi arrendado.", other.Id);
        }

        await _catalogDbContext.SaveChangesAsync();

        // Notificar ambas as partes
        await _notificationService.SendNotificationAsync(lease.TenantId, "lease",
            $"Pagamento confirmado! O teu arrendamento está ativo a partir de {lease.StartDate:dd/MM/yyyy}.", lease.Id);
        await _notificationService.SendNotificationAsync(lease.LandlordId, "lease",
            $"O inquilino efetuou o pagamento inicial. O arrendamento está ativo a partir de {lease.StartDate:dd/MM/yyyy}.", lease.Id);
    }
}
