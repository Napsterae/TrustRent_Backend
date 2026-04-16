namespace TrustRent.Shared.Contracts.Interfaces;

/// <summary>
/// Permite ao módulo de Leasing (pagamentos) ativar um lease no módulo Catalog
/// depois de pagamento confirmado.
/// </summary>
public interface ILeaseActivationService
{
    Task ActivateLeaseAfterPaymentAsync(Guid leaseId);
}
