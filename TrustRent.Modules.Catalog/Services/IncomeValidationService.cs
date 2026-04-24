using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Models.ReferenceData;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Services;

namespace TrustRent.Modules.Catalog.Services;

public class IncomeValidationService : IIncomeValidationService
{
    private const int RequiredCount = 3;
    private const int MaxAgeMonths = 4; // recibos têm de ser dos últimos 4 meses
    private const int RevalidationCooldownDays = 30; // só pode pedir nova validação após 30 dias

    private readonly CatalogDbContext _context;
    private readonly IGeminiDocumentService _geminiService;
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly ILogger<IncomeValidationService> _logger;

    public int RequiredPayslipCount => RequiredCount;

    public IncomeValidationService(
        CatalogDbContext context,
        IGeminiDocumentService geminiService,
        INotificationService notificationService,
        IUserService userService,
        ILogger<IncomeValidationService> logger)
    {
        _context = context;
        _geminiService = geminiService;
        _notificationService = notificationService;
        _userService = userService;
        _logger = logger;
    }

    public async Task RequestValidationAsync(Guid applicationId, Guid landlordId)
    {
        var application = await _context.Applications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        if (application.Property == null || application.Property.LandlordId != landlordId)
            throw new UnauthorizedAccessException("Não tens permissão para esta candidatura.");

        // Permitir pedido em InterestConfirmed (fluxo normal) ou re-pedido se já validado
        // mas a validação anterior já tem mais de RevalidationCooldownDays.
        var isRevalidation = application.IncomeRangeId.HasValue && application.IncomeValidatedAt.HasValue;
        if (isRevalidation)
        {
            var ageDays = (DateTime.UtcNow - application.IncomeValidatedAt!.Value).TotalDays;
            if (ageDays < RevalidationCooldownDays)
            {
                var daysLeft = (int)Math.Ceiling(RevalidationCooldownDays - ageDays);
                throw new InvalidOperationException(
                    $"A validação anterior é demasiado recente. Podes pedir nova validação dentro de {daysLeft} dia(s).");
            }
            if (application.Status != ApplicationStatus.InterestConfirmed
                && application.Status != ApplicationStatus.IncomeValidationRequested)
                throw new InvalidOperationException(
                    "Só podes pedir nova validação enquanto a candidatura ainda estiver em negociação.");
        }
        else if (application.Status != ApplicationStatus.InterestConfirmed)
        {
            throw new InvalidOperationException(
                "Só podes pedir validação de rendimentos depois do inquilino confirmar interesse pós-visita.");
        }

        application.Status = ApplicationStatus.IncomeValidationRequested;
        application.IsIncomeValidationRequested = true;
        application.IncomeValidationRequestedAt = DateTime.UtcNow;
        application.UpdatedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = landlordId,
            Action = isRevalidation ? "Senhorio Pediu Re-validação Rendimentos" : "Senhorio Pediu Validação Rendimentos",
            Message = isRevalidation
                ? "O senhorio pediu uma nova validação dos teus recibos de vencimento (a anterior tem mais de 1 mês)."
                : "O senhorio solicitou que enviasses os teus 3 últimos recibos de vencimento."
        });

        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            application.TenantId,
            "application",
            isRevalidation
                ? "O senhorio pediu uma nova validação dos teus recibos."
                : "O senhorio pediu validação dos teus recibos de vencimento.",
            application.Id);
    }

    public async Task<IncomeValidationResultDto> ValidatePayslipsAsync(
        Guid applicationId,
        Guid tenantId,
        IReadOnlyList<(Stream Stream, string FileName)> payslips)
    {
        if (payslips == null || payslips.Count != RequiredCount)
            throw new ArgumentException($"Tens de enviar exatamente {RequiredCount} recibos de vencimento.");

        var application = await _context.Applications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        if (application.TenantId != tenantId)
            throw new UnauthorizedAccessException("Não és o titular desta candidatura.");

        // O inquilino pode enviar recibos voluntariamente em qualquer fase de
        // negociação ativa, ou em resposta a um pedido explícito do senhorio.
        var allowedStatuses = new[]
        {
            ApplicationStatus.Pending,
            ApplicationStatus.VisitAccepted,
            ApplicationStatus.InterestConfirmed,
            ApplicationStatus.IncomeValidationRequested,
        };
        if (!allowedStatuses.Contains(application.Status))
            throw new InvalidOperationException(
                "Já não é possível submeter recibos para esta candidatura.");

        var wasRequestedByLandlord = application.Status == ApplicationStatus.IncomeValidationRequested;

        var user = await _userService.GetProfileAsync(tenantId)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");

        if (string.IsNullOrWhiteSpace(user.Nif))
            throw new InvalidOperationException(
                "Antes de validar recibos, completa a verificação de identidade no teu perfil.");

        // 1) Extrair cada recibo via IA
        var extracted = new List<ReciboVencimentoResponse>(RequiredCount);
        for (int i = 0; i < payslips.Count; i++)
        {
            var (stream, fileName) = payslips[i];
            var label = $"Recibo {i + 1}";

            ReciboVencimentoResponse response;
            try
            {
                response = await _geminiService.ExtractDocumentAsync<ReciboVencimentoResponse>(
                    stream, fileName, DocumentPrompts.ReciboVencimento);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha a extrair recibo {Index} da candidatura {ApplicationId}", i + 1, applicationId);
                throw new Exception($"[{label}] {ex.Message}");
            }

            GeminiResponseValidator.EnsureValid(response, label);

            // 2) Validar moeda — apenas aceitamos EUR (faixas salariais são em €)
            var currency = (response.Currency ?? "EUR").Trim().ToUpperInvariant();
            if (currency != "EUR" && currency != "€")
                throw new Exception($"[{label}] Moeda não suportada ({response.Currency}). Aceitamos apenas recibos em euros.");

            // 3) Validar identidade contra o utilizador
            var nifNormalized = (response.EmployeeNif ?? string.Empty).Replace(" ", "").Replace("-", "").Trim();
            if (nifNormalized != user.Nif)
                throw new Exception($"[{label}] O NIF do recibo ({MaskNif(nifNormalized)}) não corresponde ao teu NIF.");

            if (!NameMatcher.IsLikelySame(response.EmployeeName, user.Name))
                throw new Exception($"[{label}] O nome do recibo não corresponde ao teu nome.");

            if (!response.NetSalary.HasValue || response.NetSalary.Value <= 0)
                throw new Exception($"[{label}] Não foi possível ler o vencimento líquido. Tenta com o PDF original.");

            extracted.Add(response);
        }

        // 4) Validar meses distintos e dentro do intervalo permitido
        var months = extracted
            .Select(r => ParseReferenceMonth(r.ReferenceMonth))
            .ToList();

        if (months.Any(m => m == null))
            throw new Exception("Pelo menos um recibo não tem mês de referência legível.");

        var distinctMonths = months.Select(m => m!.Value).Distinct().ToList();
        if (distinctMonths.Count != RequiredCount)
            throw new Exception("Os recibos têm de ser de 3 meses diferentes.");

        var now = DateTime.UtcNow;
        var oldestAllowed = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-MaxAgeMonths);
        if (distinctMonths.Min() < oldestAllowed)
            throw new Exception($"Aceitamos apenas recibos dos últimos {MaxAgeMonths} meses.");

        // 5) Calcular média do líquido e mapear para faixa
        var avgNet = extracted.Average(r => r.NetSalary!.Value);
        var range = await ResolveRangeAsync(avgNet)
            ?? throw new Exception("Não foi possível mapear o rendimento numa faixa salarial. Contacta o suporte.");

        // 6) Persistir SÓ a faixa. Se foi pedido pelo landlord, voltar ao estado
        //    anterior (InterestConfirmed). Se foi envio voluntário, manter estado.
        application.IncomeRangeId = range.Id;
        application.IncomeValidatedAt = DateTime.UtcNow;
        if (wasRequestedByLandlord)
            application.Status = ApplicationStatus.InterestConfirmed;
        application.UpdatedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = tenantId,
            Action = "Rendimentos Validados",
            Message = wasRequestedByLandlord
                ? $"Faixa salarial validada: {range.Label}."
                : $"Inquilino validou rendimentos voluntariamente. Faixa: {range.Label}.",
            EventData = range.Code
        });

        await _context.SaveChangesAsync();

        var property = application.Property ?? await _context.Properties.FindAsync(application.PropertyId);
        if (property != null)
        {
            await _notificationService.SendNotificationAsync(
                property.LandlordId,
                "application",
                $"Validação de rendimentos concluída — faixa {range.Label}.",
                application.Id);
        }

        _logger.LogInformation(
            "Income validation OK for application {ApplicationId}: range {RangeCode}",
            application.Id, range.Code);

        return new IncomeValidationResultDto(
            application.Id,
            range.Id,
            range.Code,
            range.Label,
            application.IncomeValidatedAt!.Value
        );
    }

    public async Task<IncomeValidationResultDto> SimulateValidationAsync(Guid applicationId, Guid tenantId)
    {
        var application = await _context.Applications
            .Include(a => a.Property)
            .FirstOrDefaultAsync(a => a.Id == applicationId)
            ?? throw new KeyNotFoundException("Candidatura não encontrada.");

        if (application.TenantId != tenantId)
            throw new UnauthorizedAccessException("Não és o titular desta candidatura.");

        var allowedStatuses = new[]
        {
            ApplicationStatus.Pending,
            ApplicationStatus.VisitAccepted,
            ApplicationStatus.InterestConfirmed,
            ApplicationStatus.IncomeValidationRequested,
        };
        if (!allowedStatuses.Contains(application.Status))
            throw new InvalidOperationException("Já não é possível submeter recibos para esta candidatura.");

        var wasRequestedByLandlord = application.Status == ApplicationStatus.IncomeValidationRequested;

        var range = await _context.SalaryRanges
            .Where(r => r.IsActive)
            .OrderBy(r => r.DisplayOrder)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("Não há faixas salariais configuradas.");

        application.IncomeRangeId = range.Id;
        application.IncomeValidatedAt = DateTime.UtcNow;
        if (wasRequestedByLandlord)
            application.Status = ApplicationStatus.InterestConfirmed;
        application.UpdatedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = tenantId,
            Action = "Rendimentos Validados (DEV)",
            Message = $"[DEV] Validação simulada — faixa {range.Label}.",
            EventData = range.Code
        });

        await _context.SaveChangesAsync();

        var property = application.Property ?? await _context.Properties.FindAsync(application.PropertyId);
        if (property != null)
        {
            await _notificationService.SendNotificationAsync(
                property.LandlordId,
                "application",
                $"[DEV] Validação de rendimentos simulada — faixa {range.Label}.",
                application.Id);
        }

        _logger.LogInformation(
            "DEV income simulation for application {ApplicationId}: range {RangeCode}",
            application.Id, range.Code);

        return new IncomeValidationResultDto(
            application.Id,
            range.Id,
            range.Code,
            range.Label,
            application.IncomeValidatedAt!.Value
        );
    }

    private async Task<SalaryRange?> ResolveRangeAsync(decimal avgNet)
    {
        var ranges = await _context.SalaryRanges
            .Where(r => r.IsActive)
            .OrderBy(r => r.DisplayOrder)
            .ToListAsync();

        return ranges.FirstOrDefault(r =>
            (r.MinAmount == null || avgNet >= r.MinAmount.Value) &&
            (r.MaxAmount == null || avgNet < r.MaxAmount.Value));
    }

    /// <summary>
    /// Aceita "MM/AAAA", "M/AAAA", "AAAA-MM" ou "Outubro/2025" (limitado).
    /// Devolve o primeiro dia do mês em UTC.
    /// </summary>
    public static DateTime? ParseReferenceMonth(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;

        var trimmed = input.Trim();

        var formats = new[] { "MM/yyyy", "M/yyyy", "yyyy-MM", "yyyy/MM", "MM-yyyy" };
        if (DateTime.TryParseExact(trimmed, formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
        {
            return new DateTime(parsed.Year, parsed.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        }

        // Tentativa por nome do mês PT (ex: "Outubro/2025")
        var monthNamesPt = new[]
        {
            "janeiro","fevereiro","marco","abril","maio","junho",
            "julho","agosto","setembro","outubro","novembro","dezembro"
        };
        var lower = NameMatcher.Normalize(trimmed).Replace("/", " ").Replace("-", " ");
        var parts = lower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length >= 2)
        {
            var monthIdx = Array.IndexOf(monthNamesPt, parts[0]);
            if (monthIdx < 0) monthIdx = Array.IndexOf(monthNamesPt, parts[1]);
            if (monthIdx >= 0)
            {
                var yearPart = parts.FirstOrDefault(p => p.Length == 4 && p.All(char.IsDigit));
                if (yearPart != null && int.TryParse(yearPart, out var year))
                    return new DateTime(year, monthIdx + 1, 1, 0, 0, 0, DateTimeKind.Utc);
            }
        }

        return null;
    }

    private static string MaskNif(string nif)
    {
        if (string.IsNullOrWhiteSpace(nif) || nif.Length < 4) return "***";
        return new string('*', nif.Length - 3) + nif[^3..];
    }
}
