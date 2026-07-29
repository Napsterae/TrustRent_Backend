using System.Security.Cryptography;
using Hangfire;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Jobs;
using TrustRent.Modules.Leasing.Mappers;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared;
using TrustRent.Shared.Communications;
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
    private readonly ICommunicationContentService _communicationContentService;
    private readonly IEmailService _emailService;
    private readonly IBackgroundJobClient _backgroundJobs;
    private readonly IConfiguration? _configuration;

    public LeaseService(
        LeasingDbContext context,
        ICatalogAccessService catalogAccess,
        INotificationService notificationService,
        IContractGenerationService contractGenerationService,
        IDigitalSignatureService digitalSignatureService,
        ISignedPdfVerificationService signedPdfVerification,
        IUserService userService,
        ICommunicationContentService communicationContentService,
        IEmailService emailService,
        IBackgroundJobClient backgroundJobs,
        IConfiguration? configuration = null)
    {
        _context = context;
        _catalogAccess = catalogAccess;
        _notificationService = notificationService;
        _contractGenerationService = contractGenerationService;
        _digitalSignatureService = digitalSignatureService;
        _signedPdfVerification = signedPdfVerification;
        _userService = userService;
        _communicationContentService = communicationContentService;
        _emailService = emailService;
        _backgroundJobs = backgroundJobs;
        _configuration = configuration;
    }

    public async Task<LeaseDto> InitiateLeaseProcedureAsync(Guid applicationId, Guid userId, InitiateLeaseProcedureDto dto)
    {
        using var activity = Telemetry.Source.StartActivity("LeaseStateTransition");
        activity?.SetTag("lease.application_id", applicationId.ToString());
        var appContext = await _catalogAccess.GetApplicationContextAsync(applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        if (userId != appContext.TenantId && userId != appContext.LandlordId)
            throw new UnauthorizedAccessException("Apenas o proprietário ou o inquilino podem iniciar o procedimento de arrendamento.");

        LeaseValidator.ValidateInitiate(appContext.Status, dto.ProposedStartDate, appContext.DurationMonths, appContext.LeaseRegime);

        var endDate = dto.ProposedStartDate.AddMonths(appContext.DurationMonths);

        var lease = new Lease
        {
            PropertyId = appContext.PropertyId,
            TenantId = appContext.TenantId,
            LandlordId = appContext.LandlordId,
            ApplicationId = applicationId,
            CoTenantId = appContext.CoTenantUserId,
            GuarantorUserId = appContext.GuarantorUserId,
            GuarantorRecordId = appContext.GuarantorRecordId,
            StartDate = dto.ProposedStartDate.ToUniversalTime(),
            EndDate = endDate.ToUniversalTime(),
            DurationMonths = appContext.DurationMonths,
            AllowsRenewal = true,
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

        // ===== Signatários multi-parte =====
        var seq = 1;
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(), LeaseId = lease.Id,
            UserId = appContext.LandlordId, Role = LeaseSignatoryRole.Landlord,
            SequenceOrder = seq++
        });
        lease.Signatures.Add(new LeaseSignature
        {
            Id = Guid.NewGuid(), LeaseId = lease.Id,
            UserId = appContext.TenantId, Role = LeaseSignatoryRole.Tenant,
            SequenceOrder = seq++
        });
        if (appContext.CoTenantUserId.HasValue)
        {
            lease.Signatures.Add(new LeaseSignature
            {
                Id = Guid.NewGuid(), LeaseId = lease.Id,
                UserId = appContext.CoTenantUserId.Value, Role = LeaseSignatoryRole.CoTenant,
                SequenceOrder = seq++
            });
        }
        if (appContext.GuarantorUserId.HasValue || appContext.GuarantorRecordId.HasValue)
        {
            lease.Signatures.Add(new LeaseSignature
            {
                Id = Guid.NewGuid(), LeaseId = lease.Id,
                UserId = appContext.GuarantorUserId ?? appContext.GuarantorRecordId!.Value, Role = LeaseSignatoryRole.Guarantor,
                SequenceOrder = seq++
            });
        }
        lease.RequiredSignaturesCount = lease.Signatures.Count;

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

        // Notificar tamb\u00e9m co-candidato e fiador, se existirem
        if (appContext.CoTenantUserId.HasValue && appContext.CoTenantUserId.Value != userId)
        {
            await _notificationService.SendNotificationAsync(appContext.CoTenantUserId.Value, "lease",
                $"Foi iniciado o procedimento de arrendamento. Data proposta: {dto.ProposedStartDate:dd/MM/yyyy}.", lease.Id);
        }
        if (appContext.GuarantorUserId.HasValue && appContext.GuarantorUserId.Value != userId)
        {
            await _notificationService.SendNotificationAsync(appContext.GuarantorUserId.Value, "lease",
                $"O contrato em que és fiador foi iniciado. Data proposta: {dto.ProposedStartDate:dd/MM/yyyy}.", lease.Id);
        }
        else if (!string.IsNullOrWhiteSpace(appContext.GuarantorGuestEmail) && !string.IsNullOrWhiteSpace(appContext.GuarantorGuestAccessToken))
        {
            var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                CommunicationEmailTemplateKeys.LeaseGuarantorContractStarted,
                new Dictionary<string, string?>
                {
                    ["GuestAccessUrl"] = null,
                    ["MessageBody"] = "O processo de arrendamento em que és fiador avançou para contrato."
                });

            await _emailService.SendEmailAsync(appContext.GuarantorGuestEmail, renderedTemplate.Subject, renderedTemplate.BodyHtml);
        }

        return lease.ToDto();
    }

    public async Task<LeaseDto> ConfirmLeaseStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto)
    {
        using var activity = Telemetry.Source.StartActivity("LeaseStateTransition");
        activity?.SetTag("lease.id", leaseId.ToString());
        var lease = await _context.Leases
            .Include(l => l.History)
            .Include(l => l.Signatures)
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

        // Generate contract if official (background job) or move to signatures
        if (lease.ContractType == "Official")
        {
            lease.Status = LeaseStatus.GeneratingContract;
        }
        else
        {
            lease.Status = LeaseStatus.AwaitingSignatures;
        }

        await _context.SaveChangesAsync();

        if (lease.ContractType == "Official")
        {
            // Enqueue PDF generation as a background job
            _backgroundJobs.Enqueue<IContractGenerationJob>(job => job.GenerateContractAsync(lease.Id));

            await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.GeneratingContract, userId,
                "Data de Início Confirmada",
                $"Data: {dto.StartDate:dd/MM/yyyy}, Fim: {lease.EndDate:dd/MM/yyyy}. A gerar contrato...");

            var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
            await _notificationService.SendNotificationAsync(recipientId, "lease",
                "A data de início foi confirmada. O contrato está a ser gerado...", lease.Id);
        }
        else
        {
            await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.ContractPendingSignature, userId,
                "Data de Início Confirmada",
                $"Data: {dto.StartDate:dd/MM/yyyy}, Fim: {lease.EndDate:dd/MM/yyyy}");

            var recipientId = userId == lease.LandlordId ? lease.TenantId : lease.LandlordId;
            var nextSignature = GetNextPendingSignature(lease);

            if (nextSignature == null)
            {
                await _notificationService.SendNotificationAsync(recipientId, "lease",
                    "A data de início foi confirmada. Aguarda a aceitação dos termos do arrendamento.", lease.Id);
            }
            else
            {
                if (recipientId != nextSignature.UserId)
                {
                    await _notificationService.SendNotificationAsync(recipientId, "lease",
                        "A data de início foi confirmada. O contrato entrou em fase de aceitação.", lease.Id);
                }

                await NotifySignaturePendingAsync(lease, nextSignature,
                    "A data de início foi confirmada. É a tua vez de aceitar os termos do arrendamento.");
            }

            await NotifyExtraPartiesAsync(lease, nextSignature?.UserId ?? userId, "lease",
                "O contrato avançou. Verifica o teu painel para o próximo passo.");

            return lease.ToDto();
        }

        // Notificar co-candidato e fiador (se existirem) da transi\u00e7\u00e3o
        await NotifyExtraPartiesAsync(lease, userId, "lease",
            "O contrato avan\u00e7ou. Verifica o teu painel para o pr\u00f3ximo passo.");

        return lease.ToDto();
    }

    public async Task<LeaseDto> CounterProposeStartDateAsync(Guid leaseId, Guid userId, ConfirmLeaseStartDateDto dto)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .Include(l => l.Signatures)
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
            .Include(l => l.Signatures)
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
            .Include(l => l.Signatures)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        LeaseValidator.ValidateConfirmSignature(lease, userId);

        var result = await _digitalSignatureService.VerifyCmdSignatureAsync(dto.TransactionId, dto.OtpCode);
        if (!result.Success)
            throw new InvalidOperationException($"Falha na verificação da assinatura: {result.ErrorMessage}");

        var now = DateTime.UtcNow;

        // Legacy fields (Tenant/Landlord) preservados para compat
        if (userId == lease.LandlordId)
        {
            lease.LandlordSigned = true;
            lease.LandlordSignedAt = now;
            lease.LandlordSignatureRef = result.SignatureRef;
        }
        else if (userId == lease.TenantId)
        {
            lease.TenantSigned = true;
            lease.TenantSignedAt = now;
            lease.TenantSignatureRef = result.SignatureRef;
        }

        // Multi-parte: marca a row de assinatura correspondente
        var sig = lease.Signatures.FirstOrDefault(s => s.UserId == userId);
        if (sig != null)
        {
            sig.Signed = true;
            sig.SignedAt = now;
            sig.SignatureRef = result.SignatureRef;
            sig.SignatureVerified = true;
            sig.UpdatedAt = now;
        }

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "SignatureConfirmed",
            Message = $"Assinatura confirmada via CMD ({sig?.Role.ToString() ?? "-"}).",
            EventData = result.SignatureRef
        });

        lease.UpdatedAt = now;

        var allSigned = lease.Signatures.Count > 0
            ? lease.Signatures.All(s => s.Signed)
            : (lease.LandlordSigned && lease.TenantSigned);
        if (allSigned)
            await ActivateLeaseAsync(lease);

        await _context.SaveChangesAsync();

        if (lease.Status != LeaseStatus.AwaitingPayment)
        {
            var nextSignature = GetNextPendingSignature(lease);
            if (nextSignature != null)
            {
                await NotifySignaturePendingAsync(lease, nextSignature,
                    "Uma das partes assinou o contrato. É a tua vez de assinar.");
            }
        }

        return lease.ToDto();
    }

    public async Task<LeaseDto> AcceptLeaseTermsAsync(Guid leaseId, Guid userId, AcceptLeaseTermsDto dto)
    {
        using var activity = Telemetry.Source.StartActivity("LeaseStateTransition");
        activity?.SetTag("lease.id", leaseId.ToString());
        var lease = await _context.Leases
            .Include(l => l.History)
            .Include(l => l.Signatures)
            .Include(l => l.TermAcceptances)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        LeaseValidator.ValidateAcceptTerms(lease, userId);

        if (!dto.AcceptTerms)
            throw new ArgumentException("É necessário aceitar os termos para continuar.");

        var now = DateTime.UtcNow;

        // Legacy fields
        if (userId == lease.LandlordId)
        {
            lease.LandlordSigned = true;
            lease.LandlordSignedAt = now;
        }
        else if (userId == lease.TenantId)
        {
            lease.TenantSigned = true;
            lease.TenantSignedAt = now;
        }

        // Resolve role
        var role = userId == lease.LandlordId ? LeaseSignatoryRole.Landlord
            : userId == lease.TenantId ? LeaseSignatoryRole.Tenant
            : (lease.CoTenantId == userId ? LeaseSignatoryRole.CoTenant
            : LeaseSignatoryRole.Guarantor);

        lease.TermAcceptances.Add(new LeaseTermAcceptance
        {
            Id = Guid.NewGuid(),
            LeaseId = lease.Id,
            UserId = userId,
            Role = role,
            AcceptedAt = now,
            AcceptedDocumentHash = string.IsNullOrWhiteSpace(dto.AcceptedDocumentHash) ? null : dto.AcceptedDocumentHash.Trim()
        });

        // Também marca a assinatura como dada (em informais aceitação = assinatura)
        var sig = lease.Signatures.FirstOrDefault(s => s.UserId == userId);
        if (sig != null)
        {
            sig.Signed = true;
            sig.SignedAt = now;
            sig.SignatureVerified = true;
            sig.UpdatedAt = now;
        }

        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "TermsAccepted",
            Message = "Termos do arrendamento aceites."
        });

        lease.UpdatedAt = now;

        var allSigned = lease.Signatures.Count > 0
            ? lease.Signatures.All(s => s.Signed)
            : (lease.LandlordSigned && lease.TenantSigned);
        if (allSigned)
            await ActivateLeaseAsync(lease);

        await _context.SaveChangesAsync();

        if (lease.Status != LeaseStatus.AwaitingPayment)
        {
            var nextSignature = GetNextPendingSignature(lease);
            if (nextSignature != null)
            {
                await NotifySignaturePendingAsync(lease, nextSignature,
                    "Uma das partes aceitou os termos. É a tua vez de aceitar o contrato.");
            }
        }

        return lease.ToDto();
    }

    public async Task<LeaseDto?> GetLeaseByIdAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.Id == leaseId);

        if (lease == null) return null;
        if (!IsLeaseParty(lease, userId))
            throw new UnauthorizedAccessException("Sem permissão para aceder a este arrendamento.");

        return lease.ToDto();
    }

    public async Task<LeaseDto?> GetLeaseByApplicationIdAsync(Guid applicationId, Guid userId)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .FirstOrDefaultAsync(l => l.ApplicationId == applicationId);

        if (lease == null) return null;
        if (!IsLeaseParty(lease, userId))
            throw new UnauthorizedAccessException("Sem permissão para aceder a este arrendamento.");

        return lease.ToDto();
    }

    public async Task<LeaseSignatureStatusDto?> GetSignatureStatusAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases
            .Include(l => l.Signatures)
            .Include(l => l.TermAcceptances)
            .FirstOrDefaultAsync(l => l.Id == leaseId);
        if (lease == null) return null;
        if (!IsLeaseParty(lease, userId))
            throw new UnauthorizedAccessException("Sem permissão para aceder a este arrendamento.");

        var appContext = lease.GuarantorRecordId.HasValue && !lease.GuarantorUserId.HasValue
            ? await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId)
            : null;
        var signatories = lease.Signatures.Count > 0
            ? await BuildSignatureStatusSignatoriesAsync(lease, userId, appContext)
            : await BuildLegacySignatureStatusSignatoriesAsync(lease, userId, appContext);
        var landlordSignatory = signatories.FirstOrDefault(s => s.Role == LeaseSignatoryRole.Landlord.ToString());
        var tenantSignatory = signatories.FirstOrDefault(s => s.Role == LeaseSignatoryRole.Tenant.ToString());
        var requiredSignaturesCount = signatories.Count > 0
            ? signatories.Count
            : lease.RequiredSignaturesCount > 0
                ? lease.RequiredSignaturesCount
                : 2;

        return new LeaseSignatureStatusDto
        {
            LeaseId = lease.Id,
            LandlordSigned = landlordSignatory?.Signed ?? lease.LandlordSigned,
            LandlordSignedAt = landlordSignatory?.SignedAt ?? lease.LandlordSignedAt,
            LandlordSignatureVerified = landlordSignatory?.SignatureVerified ?? lease.LandlordSignatureVerified,
            LandlordSignatureCertSubject = landlordSignatory?.SignatureCertSubject ?? lease.LandlordSignatureCertSubject,
            TenantSigned = tenantSignatory?.Signed ?? lease.TenantSigned,
            TenantSignedAt = tenantSignatory?.SignedAt ?? lease.TenantSignedAt,
            TenantSignatureVerified = tenantSignatory?.SignatureVerified ?? lease.TenantSignatureVerified,
            TenantSignatureCertSubject = tenantSignatory?.SignatureCertSubject ?? lease.TenantSignatureCertSubject,
            ContractType = lease.ContractType,
            LeaseStatus = lease.Status.ToString(),
            RequiredSignaturesCount = requiredSignaturesCount,
            SignedCount = signatories.Count(s => s.Signed),
            Signatories = signatories
        };
    }

    public async Task<byte[]> GenerateContractAsync(Guid leaseId, Guid userId)
    {
        var lease = await _context.Leases.FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        if (!IsLeaseParty(lease, userId))
            throw new UnauthorizedAccessException("Sem permissão para aceder a este contrato.");

        if (string.IsNullOrEmpty(lease.ContractFilePath) || !File.Exists(lease.ContractFilePath))
            throw new InvalidOperationException("O contrato ainda não foi gerado.");

        return await _contractGenerationService.GetContractBytesAsync(lease.ContractFilePath);
    }

    public async Task<LeaseDto> CancelLeaseAsync(Guid leaseId, Guid userId, CancelLeaseDto dto)
    {
        using var activity = Telemetry.Source.StartActivity("LeaseStateTransition");
        activity?.SetTag("lease.id", leaseId.ToString());
        activity?.SetTag("lease.new_status", "Cancelled");
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
            .Where(l => l.TenantId == tenantId
                        || l.CoTenantId == tenantId
                        || l.GuarantorUserId == tenantId)
            .OrderByDescending(l => l.CreatedAt)
            .ToListAsync();

        return leases.Select(l => l.ToDto());
    }

    // ---- Upload Sequencial de PDF Assinado ----

    public async Task<LeaseDto> UploadSignedContractAsync(Guid leaseId, Guid userId, byte[] pdfBytes, string originalFileName)
    {
        var lease = await _context.Leases
            .Include(l => l.History)
            .Include(l => l.Signatures)
            .FirstOrDefaultAsync(l => l.Id == leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        if (!IsLeaseParty(lease, userId))
            throw new UnauthorizedAccessException("Sem permissão para fazer upload neste arrendamento.");

        var now = DateTime.UtcNow;

        var expectedRole = GetExpectedUploadRole(lease.Status)
            ?? throw new InvalidOperationException($"Este arrendamento não está numa fase de upload de assinatura (estado atual: {lease.Status}).");
        var signature = lease.Signatures
            .Where(s => !s.Signed)
            .OrderBy(s => s.SequenceOrder)
            .FirstOrDefault()
            ?? throw new InvalidOperationException("Todas as partes já assinaram este contrato.");

        if (signature.Role != expectedRole || signature.UserId != userId)
            throw new UnauthorizedAccessException("Este contrato tem de ser assinado pela próxima parte definida na sequência.");

        var expectedSignatureCount = signature.SequenceOrder;
        var verification = await _signedPdfVerification.VerifySignaturesAsync(pdfBytes, expectedSignatureCount);
        if (!verification.IsValid)
            throw new InvalidOperationException($"O documento não contém as assinaturas esperadas. {verification.ErrorMessage}");

        var baseHash = verification.PreSignatureDocumentHashes.ElementAtOrDefault(expectedSignatureCount - 1);
        var expectedBaseHash = signature.SequenceOrder == 1
            ? lease.ContractFileHash
            : lease.Signatures
                .Where(s => s.SequenceOrder == signature.SequenceOrder - 1)
                .Select(s => s.SignedFileHash)
                .FirstOrDefault();
        if (!string.IsNullOrEmpty(expectedBaseHash)
            && !string.IsNullOrEmpty(baseHash)
            && baseHash != expectedBaseHash)
        {
            throw new InvalidOperationException("O documento que assinou não corresponde à versão anterior do contrato.");
        }

        var path = SaveSignedPdf(pdfBytes, lease.Id, signature.Role.ToString().ToLowerInvariant(), originalFileName);
        var signedFileHash = Convert.ToBase64String(SHA256.HashData(pdfBytes));
        var certSubject = verification.Signatures.ElementAtOrDefault(expectedSignatureCount - 1)?.CertificateSubject
            ?? verification.Signatures.LastOrDefault()?.CertificateSubject;

        signature.Signed = true;
        signature.SignedAt = now;
        signature.SignedFilePath = path;
        signature.SignedFileHash = signedFileHash;
        signature.SignatureCertSubject = certSubject;
        signature.SignatureVerified = true;
        signature.UpdatedAt = now;

        if (signature.Role == LeaseSignatoryRole.Landlord)
        {
            lease.LandlordSignedFilePath = path;
            lease.LandlordSignedFileHash = signedFileHash;
            lease.LandlordSigned = true;
            lease.LandlordSignedAt = now;
            lease.LandlordSignatureVerified = true;
            lease.LandlordSignatureCertSubject = certSubject;
        }
        else if (signature.Role == LeaseSignatoryRole.Tenant)
        {
            lease.TenantSignedFilePath = path;
            lease.TenantSigned = true;
            lease.TenantSignedAt = now;
            lease.TenantSignatureVerified = true;
            lease.TenantSignatureCertSubject = certSubject;
        }

        lease.ContractFilePath = path;
        lease.UpdatedAt = now;
        lease.History.Add(new LeaseHistory
        {
            LeaseId = lease.Id,
            ActorId = userId,
            Action = "ContractUploaded",
            Message = $"{signature.Role} fez upload do contrato assinado. Cert: {certSubject}"
        });

        var nextSignature = GetNextPendingSignature(lease);
        if (nextSignature == null)
        {
            await ActivateLeaseAsync(lease);
        }
        else
        {
            lease.Status = GetPendingUploadStatus(nextSignature.Role);
            await _catalogAccess.UpdateApplicationStatusAsync(lease.ApplicationId, (int)ApplicationStatus.ContractPendingSignature, userId,
                "Contrato Parcialmente Assinado",
                $"Aguarda assinatura de {nextSignature.Role}.");

            await NotifySignaturePendingAsync(lease, nextSignature,
                "A parte anterior assinou o contrato. Descarrega, assina com a tua Chave Móvel Digital e faz upload.");
        }

        await _context.SaveChangesAsync();
        return lease.ToDto();
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

    private static LeaseSignatoryRole? GetExpectedUploadRole(LeaseStatus status)
        => status switch
        {
            LeaseStatus.PendingLandlordSignature => LeaseSignatoryRole.Landlord,
            LeaseStatus.PendingTenantSignature => LeaseSignatoryRole.Tenant,
            LeaseStatus.PendingCoTenantSignature => LeaseSignatoryRole.CoTenant,
            LeaseStatus.PendingGuarantorSignature => LeaseSignatoryRole.Guarantor,
            _ => null
        };

    private static LeaseStatus GetPendingUploadStatus(LeaseSignatoryRole role)
        => role switch
        {
            LeaseSignatoryRole.Landlord => LeaseStatus.PendingLandlordSignature,
            LeaseSignatoryRole.Tenant => LeaseStatus.PendingTenantSignature,
            LeaseSignatoryRole.CoTenant => LeaseStatus.PendingCoTenantSignature,
            LeaseSignatoryRole.Guarantor => LeaseStatus.PendingGuarantorSignature,
            _ => LeaseStatus.PendingTenantSignature
        };

    private static LeaseSignature? GetNextPendingSignature(Lease lease)
        => lease.Signatures
            .Where(s => !s.Signed)
            .OrderBy(s => s.SequenceOrder)
            .FirstOrDefault();

    private async Task ActivateLeaseAsync(Lease lease)
    {
        lease.Status = LeaseStatus.AwaitingPayment;
        lease.ContractSignedAt = DateTime.UtcNow;

        var initialPaymentAmount = CalculateInitialPaymentAmount(lease);

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

        // Check if tenant has a saved payment method — if not, send a dedicated notification
        var hasPaymentMethod = await _context.TenantPaymentMethods
            .AnyAsync(pm => pm.UserId == lease.TenantId && pm.IsDefault);
        if (!hasPaymentMethod)
        {
            await _notificationService.SendNotificationAsync(
                lease.TenantId,
                "payment",
                "Adiciona um método de pagamento para que a cobrança da renda mensal seja automática.",
                lease.Id);
        }

        await TrySendInitialPaymentRequiredEmailAsync(lease, lease.TenantId, initialPaymentAmount, "pendente de pagamento");
        await TrySendInitialPaymentRequiredEmailAsync(lease, lease.LandlordId, initialPaymentAmount, "aguarda pagamento inicial");

        if (lease.CoTenantId.HasValue)
            await TrySendInitialPaymentRequiredEmailAsync(lease, lease.CoTenantId.Value, initialPaymentAmount, "aguarda pagamento inicial");
        if (lease.GuarantorUserId.HasValue)
            await TrySendInitialPaymentRequiredEmailAsync(lease, lease.GuarantorUserId.Value, initialPaymentAmount, "aguarda pagamento inicial");

        await NotifyExtraPartiesAsync(
            lease,
            Guid.Empty,
            "payment",
            "O contrato foi aceite por todas as partes e entrou em fase de pagamento inicial.");
    }

    /// <summary>
    /// Determina se o utilizador é uma das partes do arrendamento (senhorio, inquilino,
    /// co-inquilino ou fiador).
    /// </summary>
    private static bool IsLeaseParty(Lease lease, Guid userId)
        => userId == lease.LandlordId
           || userId == lease.TenantId
           || (lease.CoTenantId.HasValue && lease.CoTenantId.Value == userId)
           || (lease.GuarantorUserId.HasValue && lease.GuarantorUserId.Value == userId)
           || (lease.GuarantorRecordId.HasValue && lease.GuarantorRecordId.Value == userId);

    private async Task<List<LeaseSignatoryDto>> BuildSignatureStatusSignatoriesAsync(Lease lease, Guid userId, ApplicationContext? appContext)
    {
        var signatories = new List<LeaseSignatoryDto>();

        foreach (var signature in lease.Signatures.OrderBy(s => s.SequenceOrder))
        {
            signatories.Add(await BuildSignatoryDtoAsync(
                lease,
                signature.UserId,
                signature.Role,
                signature.SequenceOrder,
                signature.Signed,
                signature.SignedAt,
                signature.SignatureVerified,
                signature.SignatureCertSubject,
                userId,
                appContext,
                useAcceptedTermsAsSignatureFallback: false));
        }

        return signatories;
    }

    private async Task<List<LeaseSignatoryDto>> BuildLegacySignatureStatusSignatoriesAsync(Lease lease, Guid userId, ApplicationContext? appContext)
    {
        var signatories = new List<LeaseSignatoryDto>();
        var sequence = 1;

        signatories.Add(await BuildSignatoryDtoAsync(
            lease,
            lease.LandlordId,
            LeaseSignatoryRole.Landlord,
            sequence++,
            lease.LandlordSigned,
            lease.LandlordSignedAt,
            lease.LandlordSignatureVerified,
            lease.LandlordSignatureCertSubject,
            userId,
            appContext,
            useAcceptedTermsAsSignatureFallback: true));

        signatories.Add(await BuildSignatoryDtoAsync(
            lease,
            lease.TenantId,
            LeaseSignatoryRole.Tenant,
            sequence++,
            lease.TenantSigned,
            lease.TenantSignedAt,
            lease.TenantSignatureVerified,
            lease.TenantSignatureCertSubject,
            userId,
            appContext,
            useAcceptedTermsAsSignatureFallback: true));

        if (lease.CoTenantId.HasValue)
        {
            signatories.Add(await BuildSignatoryDtoAsync(
                lease,
                lease.CoTenantId.Value,
                LeaseSignatoryRole.CoTenant,
                sequence++,
                false,
                null,
                false,
                null,
                userId,
                appContext,
                useAcceptedTermsAsSignatureFallback: true));
        }

        var guarantorId = lease.GuarantorUserId ?? lease.GuarantorRecordId;
        if (guarantorId.HasValue)
        {
            signatories.Add(await BuildSignatoryDtoAsync(
                lease,
                guarantorId.Value,
                LeaseSignatoryRole.Guarantor,
                sequence,
                false,
                null,
                false,
                null,
                userId,
                appContext,
                useAcceptedTermsAsSignatureFallback: true));
        }

        return signatories;
    }

    private async Task<LeaseSignatoryDto> BuildSignatoryDtoAsync(
        Lease lease,
        Guid signatoryUserId,
        LeaseSignatoryRole role,
        int sequenceOrder,
        bool signed,
        DateTime? signedAt,
        bool signatureVerified,
        string? signatureCertSubject,
        Guid userId,
        ApplicationContext? appContext,
        bool useAcceptedTermsAsSignatureFallback = false)
    {
        var acceptedTermsAt = lease.TermAcceptances.FirstOrDefault(t => t.UserId == signatoryUserId)?.AcceptedAt;
        var isGuestGuarantor = role == LeaseSignatoryRole.Guarantor
            && lease.GuarantorRecordId.HasValue
            && lease.GuarantorRecordId.Value == signatoryUserId
            && !lease.GuarantorUserId.HasValue;
        var profile = isGuestGuarantor ? null : await _userService.GetPublicProfileAsync(signatoryUserId, userId);
        var effectiveSigned = signed || (useAcceptedTermsAsSignatureFallback && acceptedTermsAt.HasValue);
        var effectiveSignedAt = effectiveSigned ? signedAt ?? acceptedTermsAt : null;
        var effectiveSignatureVerified = signatureVerified || (useAcceptedTermsAsSignatureFallback && acceptedTermsAt.HasValue);

        return new LeaseSignatoryDto
        {
            UserId = signatoryUserId,
            Name = isGuestGuarantor ? appContext?.GuarantorGuestName ?? "Fiador" : profile?.Name ?? role.ToString(),
            AvatarUrl = profile?.ProfilePictureUrl,
            Role = role.ToString(),
            SequenceOrder = sequenceOrder,
            Signed = effectiveSigned,
            SignedAt = effectiveSignedAt,
            SignatureVerified = effectiveSignatureVerified,
            SignatureCertSubject = signatureCertSubject,
            AcceptedTermsAt = acceptedTermsAt
        };
    }

    /// <summary>
    /// Notifica co-candidato e fiador (se existirem) com uma mensagem comum.
    /// </summary>
    private async Task NotifyExtraPartiesAsync(Lease lease, Guid actorId, string type, string message)
    {
        if (lease.CoTenantId.HasValue && lease.CoTenantId.Value != actorId)
        {
            await _notificationService.SendNotificationAsync(lease.CoTenantId.Value, type, message, lease.Id);
        }
        if (lease.GuarantorUserId.HasValue && lease.GuarantorUserId.Value != actorId)
        {
            await _notificationService.SendNotificationAsync(lease.GuarantorUserId.Value, type, message, lease.Id);
        }
                else if (lease.GuarantorRecordId.HasValue && lease.GuarantorRecordId.Value != actorId)
                {
                        var appContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId);
                        if (!string.IsNullOrWhiteSpace(appContext?.GuarantorGuestEmail) && !string.IsNullOrWhiteSpace(appContext.GuarantorGuestAccessToken))
                        {
                                var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                                    CommunicationEmailTemplateKeys.LeaseGuarantorContractUpdated,
                                    new Dictionary<string, string?>
                                    {
                                        ["GuestAccessUrl"] = null,
                                        ["MessageBody"] = message
                                    });
                                await _emailService.SendEmailAsync(appContext.GuarantorGuestEmail, renderedTemplate.Subject, renderedTemplate.BodyHtml);
                        }
                }
    }

        private async Task NotifySignaturePendingAsync(Lease lease, LeaseSignature signature, string message)
        {
                if (signature.Role == LeaseSignatoryRole.Guarantor && lease.GuarantorRecordId.HasValue && lease.GuarantorRecordId.Value == signature.UserId && !lease.GuarantorUserId.HasValue)
                {
                        var appContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId);
                        if (!string.IsNullOrWhiteSpace(appContext?.GuarantorGuestEmail) && !string.IsNullOrWhiteSpace(appContext.GuarantorGuestAccessToken))
                        {
                                var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                                    CommunicationEmailTemplateKeys.LeaseGuarantorSignaturePending,
                                    new Dictionary<string, string?>
                                    {
                                        ["GuestAccessUrl"] = null,
                                        ["MessageBody"] = message
                                    });
                                await _emailService.SendEmailAsync(appContext.GuarantorGuestEmail, renderedTemplate.Subject, renderedTemplate.BodyHtml);
                        }
                        return;
                }

                    await _notificationService.SendNotificationAsync(signature.UserId, "lease", message, lease.Id);
                    await TrySendSignaturePendingEmailAsync(lease, signature, message);
        }

                private async Task TrySendSignaturePendingEmailAsync(Lease lease, LeaseSignature signature, string message)
                {
                    var recipient = await _userService.GetProfileAsync(signature.UserId);
                    if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
                        return;

                    var appContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId);
                    var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                        CommunicationEmailTemplateKeys.LeaseSignaturePending,
                        new Dictionary<string, string?>
                        {
                        ["SignerRoleLabel"] = DescribeSignatoryRole(signature.Role),
                        ["PropertyTitle"] = appContext?.PropertyTitle ?? "contrato de arrendamento",
                        ["MessageBody"] = message,
                        ["LeaseUrl"] = BuildAuthenticatedLeaseUrl(signature.UserId, lease.Id)
                        });

                    await _emailService.SendEmailAsync(recipient.Email, renderedTemplate.Subject, renderedTemplate.BodyHtml);
                }

                private async Task TrySendInitialPaymentRequiredEmailAsync(Lease lease, Guid recipientId, decimal amount, string paymentStatusLabel)
                {
                    var recipient = await _userService.GetProfileAsync(recipientId);
                    if (recipient is null || string.IsNullOrWhiteSpace(recipient.Email))
                        return;

                    var appContext = await _catalogAccess.GetApplicationContextAsync(lease.ApplicationId);
                    var renderedTemplate = await _communicationContentService.RenderEmailTemplateAsync(
                        CommunicationEmailTemplateKeys.PaymentInitialRequired,
                        new Dictionary<string, string?>
                        {
                        ["PropertyTitle"] = appContext?.PropertyTitle ?? "contrato de arrendamento",
                        ["PaymentAmount"] = amount.ToString("C2", System.Globalization.CultureInfo.GetCultureInfo("pt-PT")),
                        ["PaymentTypeLabel"] = "pagamento inicial",
                        ["PaymentStatusLabel"] = paymentStatusLabel,
                        ["LeaseUrl"] = BuildAuthenticatedLeaseUrl(recipientId, lease.Id)
                        });

                    await _emailService.SendEmailAsync(recipient.Email, renderedTemplate.Subject, renderedTemplate.BodyHtml);
                }

                private decimal CalculateInitialPaymentAmount(Lease lease)
                    => lease.MonthlyRent + (lease.MonthlyRent * lease.AdvanceRentMonths) + (lease.Deposit ?? 0m);

                private string BuildAuthenticatedLeaseUrl(Guid recipientId, Guid leaseId)
                {
                    var frontendBaseUrl = _configuration?["Frontend:BaseUrl"]
                        ?? _configuration?["App:FrontendBaseUrl"]
                        ?? "http://localhost:5173";

                    return $"{frontendBaseUrl.TrimEnd('/')}/contracts";
                }

                private static string DescribeSignatoryRole(LeaseSignatoryRole role)
                    => role switch
                    {
                        LeaseSignatoryRole.Landlord => "senhorio",
                        LeaseSignatoryRole.Tenant => "inquilino",
                        LeaseSignatoryRole.CoTenant => "co-inquilino",
                        LeaseSignatoryRole.Guarantor => "fiador",
                        _ => "parte responsável"
                    };

        private string BuildGuestUrl(string token)
        {
                var frontendBaseUrl = _configuration?["Frontend:BaseUrl"]
                    ?? _configuration?["App:FrontendBaseUrl"]
                    ?? "http://localhost:5173";

                return $"{frontendBaseUrl.TrimEnd('/')}/guarantor/guest/{token}";
        }

        private static string BuildGuestLeaseEmail(string title, string message, string url)
                => $"""
                   <p style="margin:0 0 10px;font-size:12px;line-height:1.4;letter-spacing:1.8px;text-transform:uppercase;font-weight:700;color:#a65710">{title}</p>
                   <p style="margin:0 0 16px;font-size:15px;line-height:1.7;color:#334155">{message}</p>
                   <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 22px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                       <tr>
                           <td style="padding:18px 20px;background-color:#f7f3ee;background-image:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);border:1px solid #e5d6c6;border-radius:20px">
                               <p style="margin:0 0 8px;font-size:12px;line-height:1.4;letter-spacing:1.6px;text-transform:uppercase;font-weight:700;color:#1e6b66">Área segura de fiador</p>
                               <p style="margin:0;font-size:14px;line-height:1.7;color:#475569">Abre a tua área segura para acompanhar o estado do contrato e concluir o próximo passo.</p>
                           </td>
                       </tr>
                   </table>
                   <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 20px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                       <tr>
                           <td align="center" bgcolor="#1e6b66" style="border-radius:14px;background-color:#1e6b66;background-image:linear-gradient(135deg,#a65710 0%,#f2a04b 24%,#1e6b66 68%,#41b0a8 100%)">
                               <a href="{url}" style="display:inline-block;padding:13px 20px;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px;line-height:1.2;border-radius:14px">Abrir área de fiador</a>
                           </td>
                       </tr>
                   </table>
                   <p style="margin:0;font-size:12px;line-height:1.6;color:#64748b">Se o botão não funcionar, copia este endereço:<br /><span style="word-break:break-all;color:#334155">{url}</span></p>
                   """;
}
