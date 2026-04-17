using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Jobs;

public interface IContractGenerationJob
{
    Task GenerateContractAsync(Guid leaseId);
}

public class ContractGenerationJob : IContractGenerationJob
{
    private readonly LeasingDbContext _context;
    private readonly ICatalogAccessService _catalogAccess;
    private readonly IContractGenerationService _contractGenerationService;
    private readonly IUserService _userService;
    private readonly INotificationService _notificationService;

    public ContractGenerationJob(
        LeasingDbContext context,
        ICatalogAccessService catalogAccess,
        IContractGenerationService contractGenerationService,
        IUserService userService,
        INotificationService notificationService)
    {
        _context = context;
        _catalogAccess = catalogAccess;
        _contractGenerationService = contractGenerationService;
        _userService = userService;
        _notificationService = notificationService;
    }

    public async Task GenerateContractAsync(Guid leaseId)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null || lease.Status != LeaseStatus.GeneratingContract)
            return;

        try
        {
            var appContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId)
                ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

            var landlordProfile = await _userService.GetProfileAsync(lease.LandlordId);
            var tenantProfile = await _userService.GetProfileAsync(lease.TenantId);

            if (string.IsNullOrWhiteSpace(landlordProfile?.Nif) || string.IsNullOrWhiteSpace(landlordProfile?.Address))
                throw new InvalidOperationException("O contrato não pode ser gerado: o Proprietário não tem o NIF ou a Morada preenchidos no perfil.");

            if (string.IsNullOrWhiteSpace(tenantProfile?.Nif) || string.IsNullOrWhiteSpace(tenantProfile?.Address))
                throw new InvalidOperationException("O contrato não pode ser gerado: o Inquilino não tem o NIF ou a Morada preenchidos no perfil.");

            if (string.IsNullOrWhiteSpace(appContext.Street) || string.IsNullOrWhiteSpace(appContext.PostalCode) || string.IsNullOrWhiteSpace(appContext.Municipality))
                throw new InvalidOperationException("O contrato não pode ser gerado: o Imóvel não tem a Morada, Código Postal ou Localidade devidamente preenchidos.");

            var landlordName = landlordProfile?.Name ?? $"Proprietário {lease.LandlordId.ToString()[..8]}";
            var landlordNif = landlordProfile?.Nif ?? "000000000";
            var landlordAddress = landlordProfile?.Address ?? "Morada não definida";
            if (!string.IsNullOrEmpty(landlordProfile?.PostalCode))
                landlordAddress += $", {landlordProfile.PostalCode}";

            var tenantName = tenantProfile?.Name ?? $"Inquilino {lease.TenantId.ToString()[..8]}";
            var tenantNif = tenantProfile?.Nif ?? "000000000";
            var tenantAddress = tenantProfile?.Address ?? "Morada não definida";
            if (!string.IsNullOrEmpty(tenantProfile?.PostalCode))
                tenantAddress += $", {tenantProfile.PostalCode}";

            var propertyInfo = new ContractPropertyInfo
            {
                Street = appContext.Street,
                DoorNumber = appContext.DoorNumber,
                PostalCode = appContext.PostalCode,
                Parish = appContext.Parish,
                Municipality = appContext.Municipality,
                District = appContext.District,
                Typology = appContext.Typology,
                MatrixArticle = appContext.MatrixArticle,
                PropertyFraction = appContext.PropertyFraction,
                UsageLicenseNumber = appContext.UsageLicenseNumber,
                UsageLicenseDate = appContext.UsageLicenseDate,
                UsageLicenseIssuer = appContext.UsageLicenseIssuer,
                EnergyCertificateNumber = appContext.EnergyCertificateNumber,
                EnergyClass = appContext.EnergyClass
            };

            var filePath = await _contractGenerationService.GenerateContractPdfAsync(
                lease, landlordName, landlordNif, landlordAddress,
                tenantName, tenantNif, tenantAddress, propertyInfo);

            lease.ContractFilePath = filePath;
            lease.ContractGeneratedAt = DateTime.UtcNow;

            var contractBytes = await File.ReadAllBytesAsync(filePath);
            lease.ContractFileHash = Convert.ToBase64String(SHA256.HashData(contractBytes));

            lease.Status = LeaseStatus.PendingLandlordSignature;
            lease.UpdatedAt = DateTime.UtcNow;

            lease.History.Add(new LeaseHistory
            {
                LeaseId = lease.Id,
                ActorId = Guid.Empty,
                Action = "ContractGenerated",
                Message = "Contrato gerado automaticamente.",
                EventData = filePath
            });

            await _context.SaveChangesAsync();

            await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.ContractPendingSignature, Guid.Empty,
                "Contrato Gerado",
                "O contrato de arrendamento foi gerado e está pronto para assinatura.");

            await _notificationService.SendNotificationAsync(lease.LandlordId, "lease",
                "O contrato foi gerado com sucesso. Descarrega, assina e faz upload do contrato para dar início ao processo.", lease.Id);

            await _notificationService.SendNotificationAsync(lease.TenantId, "lease",
                "O contrato foi gerado com sucesso. O proprietário deve assiná-lo primeiro.", lease.Id);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Erro no Job de Geração de Contrato (Lease {leaseId}): {ex.Message}");
            throw; // Hangfire will retry automatically
        }
    }
}
