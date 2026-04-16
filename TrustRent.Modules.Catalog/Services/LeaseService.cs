using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Mappers;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Modules.Identity.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Services;

public class LeaseService : ILeaseService
{
    private readonly CatalogDbContext _context;
    private readonly INotificationService _notificationService;
    private readonly IContractGenerationService _contractGenerationService;
    private readonly IDigitalSignatureService _digitalSignatureService;
    private readonly ISignedPdfVerificationService _signedPdfVerification;
    private readonly IUserService _userService;

    public LeaseService(
        CatalogDbContext context,
        INotificationService notificationService,
        IContractGenerationService contractGenerationService,
        IDigitalSignatureService digitalSignatureService,
        ISignedPdfVerificationService signedPdfVerification,
        IUserService userService)
    {
        _context = context;
        _notificationService = notificationService;
        _contractGenerationService = contractGenerationService;
        _digitalSignatureService = digitalSignatureService;
        _signedPdfVerification = signedPdfVerification;
        _userService = userService;
    }

    public async Task<LeaseDto> InitiateLeaseProcedureAsync(Guid applicationId, Guid userId, InitiateLeaseProcedureDto dto)
    {
        var application = await _context.Applications
            .Include(a => a.Property)
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        var property = application.Property
            ?? throw new InvalidOperationException("Imóvel associado à candidatura não encontrado.");

        if (userId != application.TenantId && userId != property.LandlordId)
            throw new UnauthorizedAccessException("Apenas o proprietário ou o inquilino podem iniciar o procedimento de arrendamento.");

        LeaseValidator.ValidateInitiate(application, dto.ProposedStartDate);

        var endDate = dto.ProposedStartDate.AddMonths(application.DurationMonths);

        var lease = new Lease
        {

            PropertyId = property.Id,
            TenantId = application.TenantId,
            LandlordId = property.LandlordId,
            ApplicationId = applicationId,
            StartDate = dto.ProposedStartDate.ToUniversalTime(),
            EndDate = endDate.ToUniversalTime(),
            DurationMonths = application.DurationMonths,
            AllowsRenewal = property.AllowsRenewal,
            MonthlyRent = property.Price,
            Deposit = property.Deposit,
            AdvanceRentMonths = property.AdvanceRentMonths,
            LeaseRegime = property.LeaseRegime?.ToString(),
            ContractType = property.HasOfficialContract ? "Official" : "Informal",
            CondominiumFeesPaidBy = property.CondominiumFeesPaidBy,
            WaterPaidBy = property.WaterPaidBy,
            ElectricityPaidBy = property.ElectricityPaidBy,
            GasPaidBy = property.GasPaidBy,
            Status = LeaseStatus.Pending
        };

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "LeaseInitiated",
            Message = $"Procedimento de arrendamento iniciado. Data proposta: {dto.ProposedStartDate:dd/MM/yyyy}, Duração: {application.DurationMonths} meses."
        });

        application.Status = ApplicationStatus.LeaseStartDateProposed;
        application.UpdatedAt = DateTime.UtcNow;
        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            ApplicationId = applicationId,
            ActorId = userId,
            Action = "Procedimento de Arrendamento Iniciado",
            Message = $"Data proposta: {dto.ProposedStartDate:dd/MM/yyyy}, Duração: {application.DurationMonths} meses."
        });

        _context.Leases.Add(lease);
        await _context.SaveChangesAsync();

        // Notificar a outra parte
        var recipientId = userId == property.LandlordId ? application.TenantId : property.LandlordId;
        await _notificationService.SendNotificationAsync(
            recipientId, "lease",
            $"Foi proposta a data de início do arrendamento: {dto.ProposedStartDate:dd/MM/yyyy}.",
            lease.Id);

        return lease.ToDto();
    }

    public async Task<LeaseDto> ConfirmLeaseStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .Include(l => l.Property)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        var application = await _context.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

        LeaseValidator.ValidateConfirmStartDate(lease, userId, dto.StartDate);

        lease.StartDate = dto.StartDate.ToUniversalTime();
        lease.EndDate = dto.StartDate.AddMonths(application.DurationMonths).ToUniversalTime();
        lease.DurationMonths = application.DurationMonths;
        lease.UpdatedAt = DateTime.UtcNow;

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "StartDateConfirmed",
            Message = $"Data de início confirmada: {dto.StartDate:dd/MM/yyyy}, Duração: {application.DurationMonths} meses."
        });

        application.Status = ApplicationStatus.LeaseStartDateConfirmed;
        application.UpdatedAt = DateTime.UtcNow;
        application.History.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = userId,
            Action = "Data de Início Confirmada",
            Message = $"Data: {dto.StartDate:dd/MM/yyyy}, Fim: {lease.EndDate:dd/MM/yyyy}"
        });

        // Gerar contrato se oficial
        if (lease.ContractType == "Official")
        {
            await GenerateContractInternalAsync(lease, application);
        }

        // Mudar para a fase de assinatura sequencial
        if (lease.ContractType == "Official")
            lease.Status = LeaseStatus.PendingLandlordSignature;
        else
            lease.Status = LeaseStatus.AwaitingSignatures;

        application.Status = ApplicationStatus.ContractPendingSignature;

        await _context.SaveChangesAsync();

        var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
        var msg = lease.ContractType == "Official"
            ? "A data de início foi confirmada e o contrato foi gerado. O proprietário deve assiná-lo primeiro."
            : "A data de início foi confirmada. Aguarda a aceitação dos termos do arrendamento.";

        await _notificationService.SendNotificationAsync(recipientId, "lease", msg, lease.Id);

        // Notificar também o senhorio se for oficial (é ele quem tem de agir primeiro)
        if (lease.ContractType == "Official" && userId != lease.LandlordId)
            await _notificationService.SendNotificationAsync(lease.LandlordId, "lease",
                "A data de início foi confirmada. Descarrega, assina e faz upload do contrato para dar início ao processo.", lease.Id);

        return lease.ToDto();
    }

    public async Task<LeaseDto> CounterProposeStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .Include(l => l.Property)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        var application = await _context.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

        LeaseValidator.ValidateCounterProposeStartDate(lease, userId, dto.StartDate);

        lease.StartDate = dto.StartDate.ToUniversalTime();
        lease.EndDate = dto.StartDate.AddMonths(application.DurationMonths).ToUniversalTime();
        lease.DurationMonths = application.DurationMonths;
        lease.UpdatedAt = DateTime.UtcNow;

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "StartDateCounterProposed",
            Message = $"Nova data proposta: {dto.StartDate:dd/MM/yyyy}, Duração: {application.DurationMonths} meses."
        });

        application.UpdatedAt = DateTime.UtcNow;
        application.History.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = userId,
            Action = "Nova Data de Arrendamento Proposta",
            Message = $"Data proposta: {dto.StartDate:dd/MM/yyyy}, Duração: {application.DurationMonths} meses."
        });

        await _context.SaveChangesAsync();

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

        // Obter email do utilizador (aproximação: usar userId como placeholder — em produção passar do contexto)
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

        var application = await _context.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

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
            Message = $"Assinatura confirmada via CMD.",
            EventData = result.SignatureRef
        });

        lease.UpdatedAt = now;

        if (lease.LandlordSigned && lease.TenantSigned)
            await ActivateLeaseAsync(lease, application, now);

        await _context.SaveChangesAsync();

        if (lease.Status == LeaseStatus.AwaitingPayment)
        {
            // As notificações de pagamento são enviadas dentro do ActivateLeaseAsync
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

        var application = await _context.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

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
            await ActivateLeaseAsync(lease, application, now);

        await _context.SaveChangesAsync();

        if (lease.Status == LeaseStatus.AwaitingPayment)
        {
            // As notificações de pagamento são enviadas dentro do ActivateLeaseAsync
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
            .Include(l => l.Property)
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
        var lease = await _context.Leases
            .Include(l => l.Property)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
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

        var application = await _context.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

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

        application.Status = ApplicationStatus.Rejected;
        application.UpdatedAt = DateTime.UtcNow;
        application.History.Add(new ApplicationHistory
        {

            ApplicationId = application.Id,
            ActorId = userId,
            Action = "Arrendamento Cancelado",
            Message = dto.Reason
        });

        await _context.SaveChangesAsync();

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

        var application = await _context.Applications
            .Include(a => a.History)
            .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId)
            ?? throw new KeyNotFoundException("Candidatura associada não encontrada.");

        if (userId != lease.LandlordId && userId != lease.TenantId)
            throw new UnauthorizedAccessException("Sem permissão para fazer upload neste arrendamento.");

        var now = DateTime.UtcNow;

        // ---- FASE 1: Senhorio assina e faz upload ----
        if (lease.Status == LeaseStatus.PendingLandlordSignature)
        {
            if (userId != lease.LandlordId)
                throw new UnauthorizedAccessException(
                    "Apenas o proprietário pode fazer upload do contrato nesta fase. O inquilino deve aguardar.");

            var verification = await _signedPdfVerification.VerifySignaturesAsync(pdfBytes, 1);
            if (!verification.IsValid)
                throw new InvalidOperationException(
                    $"O documento não contém uma assinatura digital válida. {verification.ErrorMessage}");

            // ---- Verificar integridade: o PDF assinado deve ter sido derivado do contrato original ----
            // GetRevision devolve os bytes do documento tal como estavam antes da assinatura ser aplicada.
            // Comparamos o hash desses bytes com o hash do contrato original guardado na BD.
            var signedDocHash = verification.PreSignatureDocumentHashes.FirstOrDefault();
            if (!string.IsNullOrEmpty(lease.ContractFileHash) && !string.IsNullOrEmpty(signedDocHash))
            {
                if (signedDocHash != lease.ContractFileHash)
                    throw new InvalidOperationException(
                        "O documento que assinou não corresponde ao contrato gerado pela plataforma. " +
                        "Por favor descarrega o contrato original da plataforma, assina esse ficheiro e volta a fazer upload.");
            }

            var path = SaveSignedPdf(pdfBytes, lease.Id, "landlord", originalFileName);
            lease.LandlordSignedFilePath = path;
            lease.LandlordSigned = true;
            lease.LandlordSignedAt = now;
            lease.LandlordSignatureVerified = true;
            lease.LandlordSignatureCertSubject = verification.Signatures.FirstOrDefault()?.CertificateSubject;
            // Guardar hash do PDF assinado pelo senhorio — o inquilino deve assinar exactamente este ficheiro
            lease.LandlordSignedFileHash = Convert.ToBase64String(SHA256.HashData(pdfBytes));
            lease.Status = LeaseStatus.PendingTenantSignature;
            lease.UpdatedAt = now;

            lease.History.Add(new LeaseHistory
            {
                LeaseId = lease.Id, ActorId = userId,
                Action = "LandlordContractUploaded",
                Message = $"Proprietário fez upload do contrato assinado. Cert: {lease.LandlordSignatureCertSubject}"
            });

            application.History.Add(new ApplicationHistory
            {
                ApplicationId = application.Id, ActorId = userId,
                Action = "Contrato Assinado pelo Proprietário",
                Message = "O proprietário fez upload do contrato assinado digitalmente. O inquilino deve agora assinar."
            });

            await _context.SaveChangesAsync();

            await _notificationService.SendNotificationAsync(lease.TenantId, "lease",
                "O proprietário assinou o contrato. Descarrega o documento, assina-o com a tua Chave Móvel Digital e faz upload.",
                lease.Id);

            return lease.ToDto();
        }

        // ---- FASE 2: Inquilino assina e faz upload ----
        if (lease.Status == LeaseStatus.PendingTenantSignature)
        {
            if (userId != lease.TenantId)
                throw new UnauthorizedAccessException(
                    "Apenas o inquilino pode fazer upload do contrato nesta fase.");

            var verificationT = await _signedPdfVerification.VerifySignaturesAsync(pdfBytes, 2);
            if (!verificationT.IsValid)
                throw new InvalidOperationException(
                    $"O documento deve conter a assinatura do proprietário e a tua. {verificationT.ErrorMessage}");

            // ---- Verificar integridade: o documento que o inquilino assinou deve ser o PDF do senhorio ----
            // A 2ª assinatura foi aplicada sobre o PDF já assinado pelo senhorio.
            // O hash da revisão antes da 2ª assinatura deve coincidir com o hash do PDF do senhorio.
            var tenantBaseHash = verificationT.PreSignatureDocumentHashes.ElementAtOrDefault(1);
            if (!string.IsNullOrEmpty(lease.LandlordSignedFileHash) && !string.IsNullOrEmpty(tenantBaseHash))
            {
                if (tenantBaseHash != lease.LandlordSignedFileHash)
                    throw new InvalidOperationException(
                        "O documento que assinou não é o mesmo que o proprietário assinou. " +
                        "Por favor descarrega o documento (já assinado pelo proprietário) da plataforma, assina esse ficheiro e volta a fazer upload.");
            }

            var path = SaveSignedPdf(pdfBytes, lease.Id, "final", originalFileName);
            lease.TenantSignedFilePath = path;
            lease.ContractFilePath = path; // documento final substitui o original
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

            application.History.Add(new ApplicationHistory
            {
                ApplicationId = application.Id, ActorId = userId,
                Action = "Contrato Assinado pelo Inquilino",
                Message = "O inquilino assinou o contrato. Ambas as assinaturas verificadas — arrendamento ativo."
            });

            await ActivateLeaseAsync(lease, application, now);
            await _context.SaveChangesAsync();

            // Notificações de pagamento são enviadas dentro do ActivateLeaseAsync

            return lease.ToDto();
        }

        throw new InvalidOperationException(
            $"Este arrendamento não está numa fase de upload de assinatura (estado atual: {lease.Status}).");
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

    // ---- Private helpers ----

    private async Task GenerateContractInternalAsync(Lease lease, Application application)
    {
        var landlordProfile = await _userService.GetProfileAsync(lease.LandlordId);
        var tenantProfile = await _userService.GetProfileAsync(lease.TenantId);

        if (string.IsNullOrWhiteSpace(landlordProfile?.Nif) || string.IsNullOrWhiteSpace(landlordProfile?.Address))
            throw new InvalidOperationException("O contrato não pode ser gerado: o Proprietário não tem o NIF ou a Morada preenchidos no perfil.");

        if (string.IsNullOrWhiteSpace(tenantProfile?.Nif) || string.IsNullOrWhiteSpace(tenantProfile?.Address))
            throw new InvalidOperationException("O contrato não pode ser gerado: o Inquilino não tem o NIF ou a Morada preenchidos no perfil.");

        var property = lease.Property;
        if (property != null)
        {
            if (string.IsNullOrWhiteSpace(property.Street) || string.IsNullOrWhiteSpace(property.PostalCode) || string.IsNullOrWhiteSpace(property.Municipality))
                throw new InvalidOperationException("O contrato não pode ser gerado: o Imóvel não tem a Morada, Código Postal ou Localidade devidamente preenchidos.");
        }

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

        var filePath = await _contractGenerationService.GenerateContractPdfAsync(
            lease, landlordName, landlordNif, landlordAddress,
            tenantName, tenantNif, tenantAddress);

        lease.ContractFilePath = filePath;
        lease.ContractGeneratedAt = DateTime.UtcNow;

        // Guardar hash do ficheiro original para validar integridade no upload
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

        application.History.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = Guid.Empty,
            Action = "Contrato Gerado",
            Message = "O contrato de arrendamento foi gerado e está pronto para assinatura."
        });
    }

    private async Task ActivateLeaseAsync(Lease lease, Application application, DateTime now)
    {
        // Em vez de ativar diretamente, colocar em AwaitingPayment
        // O lease será ativado pelo CatalogLeaseActivationService após pagamento confirmado
        lease.Status = LeaseStatus.AwaitingPayment;
        lease.ContractSignedAt = now;

        lease.History.Add(new LeaseHistory
        {

            LeaseId = lease.Id,
            ActorId = Guid.Empty,
            Action = "AwaitingPayment",
            Message = "Contrato assinado/aceite por ambas as partes. Aguarda pagamento inicial do inquilino."
        });

        application.Status = ApplicationStatus.AwaitingPayment;
        application.UpdatedAt = now;
        application.History.Add(new ApplicationHistory
        {

            ApplicationId = application.Id,
            ActorId = Guid.Empty,
            Action = "Aguarda Pagamento",
            Message = "Termos aceites por ambas as partes. O inquilino deve efetuar o pagamento inicial para ativar o arrendamento."
        });

        // Notificar inquilino que deve efetuar o pagamento
        await _notificationService.SendNotificationAsync(lease.TenantId, "payment",
            "O contrato foi aceite por ambas as partes. Efetua o pagamento inicial para ativar o arrendamento.", lease.Id);
        await _notificationService.SendNotificationAsync(lease.LandlordId, "payment",
            "O contrato foi aceite por ambas as partes. Aguarda o pagamento inicial do inquilino.", lease.Id);
    }
}
