using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Mappers;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.DTOs;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Services;

public class LeaseService : ILeaseService
{
    private readonly LeasingDbContext _context;
    private readonly ICatalogAccessService _catalogAccess;
    private readonly INotificationService _notificationService;
    private readonly IContractGenerationService _contractGenerationService;
    private readonly IDigitalSignatureService _digitalSignatureService;
    private readonly ISignedPdfVerificationService _signedPdfVerification;
    private readonly IUserService _userService;

    public LeaseService(
        LeasingDbContext context,
        ICatalogAccessService catalogAccess,
        INotificationService notificationService,
        IContractGenerationService contractGenerationService,
        IDigitalSignatureService digitalSignatureService,
        ISignedPdfVerificationService signedPdfVerification,
        IUserService userService)
    {
        _context = context;
        _catalogAccess = catalogAccess;
        _notificationService = notificationService;
        _contractGenerationService = contractGenerationService;
        _digitalSignatureService = digitalSignatureService;
        _signedPdfVerification = signedPdfVerification;
        _userService = userService;
    }

    public async Task<LeaseDto> InitiateLeaseProcedureAsync(Guid applicationId, Guid userId, InitiateLeaseProcedureDto dto)
    {
        var appContext = await _catalogAccess.GetApplicationContextAsync(applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        if (userId != appContext.TenantId && userId != appContext.LandlordId)
            throw new UnauthorizedAccessException("Apenas o proprietário ou o inquilino podem iniciar o procedimento de arrendamento.");

        LeaseValidator.ValidateInitiate(appContext.Status, dto.ProposedStartDate);

        var endDate = dto.ProposedStartDate.AddMonths(appContext.DurationMonths);

        var lease = new Lease
        {
            PropertyId = appContext.PropertyId,
            TenantId = appContext.TenantId,
            LandlordId = appContext.LandlordId,
            ApplicationId = applicationId,
            StartDate = dto.ProposedStartDate.ToUniversalTime(),
            EndDate = endDate.ToUniversalTime(),
            DurationMonths = appContext.DurationMonths,
            AllowsRenewal = appContext.AllowsRenewal,
            MonthlyRent = appContext.Price,
            Deposit = appContext.Deposit,
            AdvanceRentMonths = appContext.AdvanceRentMonths,
            LeaseRegime = appContext.LeaseRegime,
            ContractType = appContext.HasOfficialContract ? "Official" : "Informal",
            CondominiumFeesPaidBy = appContext.CondominiumFeesPaidBy,
            WaterPaidBy = appContext.WaterPaidBy,
            ElectricityPaidBy = appContext.ElectricityPaidBy,
            GasPaidBy = appContext.GasPaidBy,
            Status = LeaseStatus.Pending
        };

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "LeaseInitiated",
            Message = $"Procedimento de arrendamento iniciado. Data proposta: {dto.ProposedStartDate:dd/MM/yyyy}, Duração: {appContext.DurationMonths} meses."
        });

        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Update application status via cross-module access
        await _catalogAccess.UpdateApplicationStatusAsync(applicationId, (int)ApplicationStatus.LeaseStartDateProposed, userId,
            "Procedimento de Arrendamento Iniciado",
            $"Data proposta: {dto.ProposedStartDate:dd/MM/yyyy}, Duração: {appContext.DurationMonths} meses.");

        var recipientId = userId == appContext.LandlordId ? appContext.TenantId : appContext.LandlordId;
        await _notificationService.SendNotificationAsync(recipientId, "lease",
            $"Foi proposta a data de início do arrendamento: {dto.ProposedStartDate:dd/MM/yyyy}.", lease.Id);

        return lease.ToDto();
    }

    public async Task<LeaseDto> ConfirmLeaseStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        var appContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

        LeaseValidator.ValidateConfirmStartDate(lease, userId, dto.StartDate);

        lease.StartDate = dto.StartDate.ToUniversalTime();
        lease.EndDate = dto.StartDate.AddMonths(appContext.DurationMonths).ToUniversalTime();
        lease.DurationMonths = appContext.DurationMonths;
        lease.UpdatedAt = DateTime.UtcNow;

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "StartDateConfirmed",
            Message = $"Data de início confirmada: {dto.StartDate:dd/MM/yyyy}, Duração: {appContext.DurationMonths} meses."
        });

        // Generate contract if official
        if (lease.ContractType == "Official")
        {
            await GenerateContractInternalAsync(lease, appContext);
        }

        // Move to signature phase
        if (lease.ContractType == "Official")
            lease.Status = LeaseStatus.PendingLandlordSignature;
        else
            lease.Status = LeaseStatus.AwaitingSignatures;

        await _context.SaveChangesAsync();

        await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.ContractPendingSignature, userId,
            "Data de Início Confirmada",
            $"Data: {dto.StartDate:dd/MM/yyyy}, Fim: {lease.EndDate:dd/MM/yyyy}");

        var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
        var msg = lease.ContractType == "Official"
            ? "A data de início foi confirmada e o contrato foi gerado. O proprietário deve assiná-lo primeiro."
            : "A data de início foi confirmada. Aguarda a aceitação dos termos do arrendamento.";

        await _notificationService.SendNotificationAsync(recipientId, "lease", msg, lease.Id);

        if (lease.ContractType == "Official" && userId != lease.LandlordId)
            await _notificationService.SendNotificationAsync(lease.LandlordId, "lease",
                "A data de início foi confirmada. Descarrega, assina e faz upload do contrato para dar início ao processo.", lease.Id);

        return lease.ToDto();
    }

    public async Task<LeaseDto> CounterProposeStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        var appContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

        LeaseValidator.ValidateCounterProposeStartDate(lease, userId, dto.StartDate);

        lease.StartDate = dto.StartDate.ToUniversalTime();
        lease.EndDate = dto.StartDate.AddMonths(appContext.DurationMonths).ToUniversalTime();
        lease.DurationMonths = appContext.DurationMonths;
        lease.UpdatedAt = DateTime.UtcNow;

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "StartDateCounterProposed",
            Message = $"Nova data proposta: {dto.StartDate:dd/MM/yyyy}, Duração: {appContext.DurationMonths} meses."
        });

        await _context.SaveChangesAsync();

        await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.LeaseStartDateProposed, userId,
            "Nova Data de Arrendamento Proposta",
            $"Data proposta: {dto.StartDate:dd/MM/yyyy}, Duração: {appContext.DurationMonths} meses.");

        var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
        await _notificationService.SendNotificationAsync(recipientId, "lease",
            "Foi sugerida uma nova data para o início do arrendamento.", lease.Id);

        return lease.ToDto();
    }

    public async Task<LeaseDto> RequestSignatureAsync(Guid leaseId, Guid userId, RequestLeaseSignatureDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        LeaseValidator.ValidateRequestSignature(lease, userId, dto.PhoneNumber);

        if (string.IsNullOrEmpty(lease.ContractFilePath) || !File.Exists(lease.ContractFilePath))
            throw new InvalidOperationException("O contrato ainda não foi gerado. Por favor confirme a data de início primeiro.");

        var contractBytes = await _contractGenerationService.GetContractBytesAsync(lease.ContractFilePath);
        var documentHash = Convert.ToBase64String(SHA256.HashData(contractBytes));

        var userEmail = $"{userId}@trustrent.local";

        var result = await _digitalSignatureService.InitiateCmdSignatureAsync(documentHash, dto.PhoneNumber, userEmail);
        if (!result.Success)
            throw new InvalidOperationException($"Falha ao iniciar assinatura CMD: {result.ErrorMessage}");

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "SignatureRequested",
            Message = $"Assinatura CMD solicitada para {dto.PhoneNumber}.",
            EventData = result.ProcessId
        });

        lease.UpdatedAt = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return lease.ToDto();
    }

    public async Task<LeaseDto> ConfirmSignatureAsync(Guid leaseId, Guid userId, ConfirmLeaseSignatureDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        LeaseValidator.ValidateConfirmSignature(lease, userId);

        var result = await _digitalSignatureService.VerifyCmdSignatureAsync(dto.TransactionId, dto.OtpCode);
        if (!result.Success)
            throw new InvalidOperationException($"Falha na verificação da assinatura: {result.ErrorMessage}");

        var now = DateTime.UtcNow;
        if (userId == lease.LandlordId)
        {
            lease.LandlordSigned = true;
            lease.LandlordSignedAt = now;
            lease.LandlordSignatureRef = result.SignatureRef;
        }
        else
        {
            lease.TenantSigned = true;
            lease.TenantSignedAt = now;
            lease.TenantSignatureRef = result.SignatureRef;
        }

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "SignatureConfirmed",
            Message = "Assinatura confirmada via CMD.",
            EventData = result.SignatureRef
        });

        lease.UpdatedAt = now;

        if (lease.LandlordSigned && lease.TenantSigned)
            await ActivateLeaseAsync(lease);

        await _context.SaveChangesAsync();

        if (lease.Status == LeaseStatus.AwaitingPayment)
        {
            // Notifications sent inside ActivateLeaseAsync
        }
        else
        {
            var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
            await _notificationService.SendNotificationAsync(recipientId, "lease",
                "A outra parte assinou o contrato. Aguarda a tua assinatura.", lease.Id);
        }

        return lease.ToDto();
    }

    public async Task<LeaseDto> AcceptLeaseTermsAsync(Guid leaseId, Guid userId, AcceptLeaseTermsDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        LeaseValidator.ValidateAcceptTerms(lease, userId);

        if (!dto.AcceptTerms)
            throw new ArgumentException("É necessário aceitar os termos para continuar.");

        var now = DateTime.UtcNow;
        if (userId == lease.LandlordId)
        {
            lease.LandlordSigned = true;
            lease.LandlordSignedAt = now;
        }
        else
        {
            lease.TenantSigned = true;
            lease.TenantSignedAt = now;
        }

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "TermsAccepted",
            Message = "Termos do arrendamento aceites."
        });

        lease.UpdatedAt = now;

        if (lease.LandlordSigned && lease.TenantSigned)
            await ActivateLeaseAsync(lease);

        await _context.SaveChangesAsync();

        if (lease.Status == LeaseStatus.AwaitingPayment)
        {
            // Notifications sent inside ActivateLeaseAsync
        }
        else
        {
            var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
            await _notificationService.SendNotificationAsync(recipientId, "lease",
                "A outra parte aceitou os termos. Aguarda a tua aceitação.", lease.Id);
        }

        return lease.ToDto();
    }

    public async Task<LeaseDto?> GetLeaseByIdAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null) return null;
        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Sem permissão para aceder a este arrendamento.");

        return lease.ToDto();
    }

    public async Task<LeaseDto?> GetLeaseByApplicationIdAsync(Guid applicationId, Guid userId)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.ApplicationId == applicationId);

        if (lease == null) return null;
        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Sem permissão para aceder a este arrendamento.");

        return lease.ToDto();
    }

    public async Task<LeaseSignatureStatusDto?> GetSignatureStatusAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases.FirstOrDefaultAsync(l => l.Id == leaseId);
        if (lease == null) return null;
        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Sem permissão para aceder a este arrendamento.");

        return new LeaseSignatureStatusDto
        {
            LeaseId = lease.Id,
            LandlordSigned = lease.LandlordSigned,
            LandlordSignedAt = lease.LandlordSignedAt,
            TenantSigned = lease.TenantSigned,
            TenantSignedAt = lease.TenantSignedAt,
            ContractType = lease.ContractType,
            LeaseStatus = lease.Status.ToString()
        };
    }

    public async Task<byte[]> GenerateContractAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases.FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Sem permissão para aceder a este contrato.");

        if (string.IsNullOrEmpty(lease.ContractFilePath) || !File.Exists(lease.ContractFilePath))
            throw new InvalidOperationException("O contrato ainda não foi gerado.");

        return await _contractGenerationService.GetContractBytesAsync(lease.ContractFilePath);
    }

    public async Task<LeaseDto> CancelLeaseAsync(Guid leaseId, Guid userId, CancelLeaseDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        LeaseValidator.ValidateCancel(lease, userId);

        lease.Status = LeaseStatus.Cancelled;
        lease.UpdatedAt = DateTime.UtcNow;

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "LeaseCancelled",
            Message = dto.Reason
        });

        await _context.SaveChangesAsync();

        await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.Rejected, userId,
            "Arrendamento Cancelado", dto.Reason);

        var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
        await _notificationService.SendNotificationAsync(recipientId, "lease",
            "O procedimento de arrendamento foi cancelado.", lease.Id);

        return lease.ToDto();
    }

    public async Task<IEnumerable<LeaseDto>> GetLeasesForTenantAsync(Guid tenantId)
    {
        var leases = await _context.Leases
            .Include(l => l.History)
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return leases.Select(l => l.ToDto());
    }

    // ---- Upload Sequencial de PDF Assinado ----

    public async Task<LeaseDto> UploadSignedContractAsync(Guid leaseId, Guid userId, byte[] pdfBytes, string originalFileName)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        if (userId != lease.LandlordId && userId != lease.TenantId)
            throw new UnauthorizedAccessException("Sem permissão para fazer upload neste arrendamento.");

        var now = DateTime.UtcNow;

        // ---- FASE 1: Senhorio assina e faz upload ----
        if (lease.Status == LeaseStatus.PendingLandlordSignature)
        {
            if (userId != lease.LandlordId)
                throw new UnauthorizedAccessException("Apenas o proprietário pode fazer upload do contrato nesta fase.");

            var verification = await _signedPdfVerification.VerifySignaturesAsync(pdfBytes, 1);
            if (!verification.IsValid)
                throw new InvalidOperationException($"O documento não contém uma assinatura digital válida. {verification.ErrorMessage}");

            var signedDocHash = verification.PreSignatureDocumentHashes.FirstOrDefault();
            if (!string.IsNullOrEmpty(lease.ContractFileHash) && !string.IsNullOrEmpty(signedDocHash))
            {
                if (signedDocHash != lease.ContractFileHash)
                    throw new InvalidOperationException(
                        "O documento que assinou não corresponde ao contrato gerado pela plataforma.");
            }

            var path = SaveSignedPdf(pdfBytes, lease.Id, "landlord", originalFileName);
            lease.LandlordSignedFilePath = path;
            lease.LandlordSigned = true;
            lease.LandlordSignedAt = now;
            lease.LandlordSignatureVerified = true;
            lease.LandlordSignatureCertSubject = verification.Signatures.FirstOrDefault()?.CertificateSubject;
            lease.LandlordSignedFileHash = Convert.ToBase64String(SHA256.HashData(pdfBytes));
            lease.Status = LeaseStatus.PendingTenantSignature;
            lease.UpdatedAt = now;

            lease.History.Add(new LeaseHistory
            {
                LeaseId = lease.Id, ActorId = userId,
                Action = "LandlordContractUploaded",
                Message = $"Proprietário fez upload do contrato assinado. Cert: {lease.LandlordSignatureCertSubject}"
            });

            await _context.SaveChangesAsync();

            await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.ContractPendingSignature, userId,
                "Contrato Assinado pelo Proprietário",
                "O proprietário fez upload do contrato assinado digitalmente. O inquilino deve agora assinar.");

            await _notificationService.SendNotificationAsync(lease.TenantId, "lease",
                "O proprietário assinou o contrato. Descarrega o documento, assina-o com a tua Chave Móvel Digital e faz upload.",
                lease.Id);

            return lease.ToDto();
        }

        // ---- FASE 2: Inquilino assina e faz upload ----
        if (lease.Status == LeaseStatus.PendingTenantSignature)
        {
            if (userId != lease.TenantId)
                throw new UnauthorizedAccessException("Apenas o inquilino pode fazer upload do contrato nesta fase.");

            var verificationT = await _signedPdfVerification.VerifySignaturesAsync(pdfBytes, 2);
            if (!verificationT.IsValid)
                throw new InvalidOperationException($"O documento deve conter a assinatura do proprietário e a tua. {verificationT.ErrorMessage}");

            var tenantBaseHash = verificationT.PreSignatureDocumentHashes.ElementAtOrDefault(1);
            if (!string.IsNullOrEmpty(lease.LandlordSignedFileHash) && !string.IsNullOrEmpty(tenantBaseHash))
            {
                if (tenantBaseHash != lease.LandlordSignedFileHash)
                    throw new InvalidOperationException(
                        "O documento que assinou não é o mesmo que o proprietário assinou.");
            }

            var path = SaveSignedPdf(pdfBytes, lease.Id, "final", originalFileName);
            lease.TenantSignedFilePath = path;
            lease.ContractFilePath = path;
            lease.TenantSigned = true;
            lease.TenantSignedAt = now;
            lease.TenantSignatureVerified = true;
            lease.TenantSignatureCertSubject = verificationT.Signatures.LastOrDefault()?.CertificateSubject;
            lease.ContractSignedAt = now;
            lease.UpdatedAt = now;

            lease.History.Add(new LeaseHistory
            {
                LeaseId = lease.Id, ActorId = userId,
                Action = "TenantContractUploaded",
                Message = $"Inquilino fez upload do contrato com ambas as assinaturas. Cert: {lease.TenantSignatureCertSubject}"
            });

            await ActivateLeaseAsync(lease);
            await _context.SaveChangesAsync();

            await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.AwaitingPayment, Guid.Empty,
                "Aguarda Pagamento",
                "Termos aceites por ambas as partes. O inquilino deve efetuar o pagamento inicial para ativar o arrendamento.");

            return lease.ToDto();
        }

        throw new InvalidOperationException($"Este arrendamento não está numa fase de upload de assinatura (estado atual: {lease.Status}).");
    }

    public async Task<byte[]?> GetLandlordSignedContractAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases.FirstOrDefaultAsync(l => l.Id == leaseId);
        if (lease == null) return null;
        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Sem permissão.");
        if (string.IsNullOrEmpty(lease.LandlordSignedFilePath) || !File.Exists(lease.LandlordSignedFilePath))
            return null;
        return await File.ReadAllBytesAsync(lease.LandlordSignedFilePath);
    }

    private static string SaveSignedPdf(byte[] pdfBytes, Guid leaseId, string party, string originalFileName)
    {
        var dir = Path.Combine("contracts", "signed", leaseId.ToString());
        Directory.CreateDirectory(dir);
        var fileName = $"{party}_{DateTime.UtcNow:yyyyMMddHHmmss}.pdf";
        var path = Path.Combine(dir, fileName);
        File.WriteAllBytes(path, pdfBytes);
        return path;
    }

    private async Task GenerateContractInternalAsync(Lease lease, ApplicationContext appContext)
    {
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

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = Guid.Empty,
            Action = "ContractGenerated",
            Message = "Contrato gerado automaticamente.",
            EventData = filePath
        });

        await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.ContractPendingSignature, Guid.Empty,
            "Contrato Gerado",
            "O contrato de arrendamento foi gerado e está pronto para assinatura.");
    }

    private async Task ActivateLeaseAsync(Lease lease)
    {
        lease.Status = LeaseStatus.AwaitingPayment;
        lease.ContractSignedAt = DateTime.UtcNow;

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = Guid.Empty,
            Action = "AwaitingPayment",
            Message = "Contrato assinado/aceite por ambas as partes. Aguarda pagamento inicial do inquilino."
        });

        await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.AwaitingPayment, Guid.Empty,
            "Aguarda Pagamento",
            "Termos aceites por ambas as partes. O inquilino deve efetuar o pagamento inicial para ativar o arrendamento.");

        await _notificationService.SendNotificationAsync(lease.TenantId, "payment",
            "O contrato foi aceite por ambas as partes. Efetua o pagamento inicial para ativar o arrendamento.", lease.Id);
        await _notificationService.SendNotificationAsync(lease.LandlordId, "payment",
            "O contrato foi aceite por ambas as partes. Aguarda o pagamento inicial do inquilino.", lease.Id);
    }
}
