using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Models.ReferenceData;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Services;

namespace TrustRent.Modules.Catalog.Services;

public class IncomeValidationService : IIncomeValidationService
{
    // Política de recibos: até 3, dos últimos 4 meses, em meses distintos.
    // Quem tem <3 (emprego há pouco tempo) compensa com declaração da entidade empregadora.
    private const int MaxPayslips = 3;
    private const int MinPayslipsToSkipDeclaration = 3;
    private const int MaxAgeMonths = 4;
    private const int RevalidationCooldownDays = 30;
    private const int DeclarationMaxAgeDays = 90; // declaração emitida há ≤ 3 meses

    private readonly CatalogDbContext _context;
    private readonly IGeminiDocumentService _geminiService;
    private readonly INotificationService _notificationService;
    private readonly IUserService _userService;
    private readonly ILogger<IncomeValidationService> _logger;

    public int MaxPayslipCount => MaxPayslips;
    public int PayslipsToSkipDeclaration => MinPayslipsToSkipDeclaration;

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
                ? "O senhorio pediu uma nova validação dos teus rendimentos (a anterior tem mais de 1 mês)."
                : "O senhorio solicitou que enviasses comprovativos de rendimento (até 3 recibos ou declaração de empregador / atividade)."
        });

        await _context.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            application.TenantId,
            "application",
            isRevalidation
                ? "O senhorio pediu uma nova validação dos teus rendimentos."
                : "O senhorio pediu validação dos teus rendimentos.",
            application.Id);
    }

    public async Task<IncomeValidationResultDto> ValidateAsync(
        Guid applicationId,
        Guid tenantId,
        IncomeValidationSubmissionDto submission)
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
            throw new InvalidOperationException(
                "Já não é possível submeter comprovativos de rendimento para esta candidatura.");

        var wasRequestedByLandlord = application.Status == ApplicationStatus.IncomeValidationRequested;

        var user = await _userService.GetProfileAsync(tenantId)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");

        if (string.IsNullOrWhiteSpace(user.Nif))
            throw new InvalidOperationException(
                "Antes de validar rendimentos, completa a verificação de identidade no teu perfil.");

        decimal averageMonthly;
        IncomeValidationMethod method;
        string? employerName;
        string? employerNif = null;
        DateTime? employmentStartDate;
        int payslipsCount;

        if (submission.EmploymentType == EmploymentType.Employee)
        {
            (averageMonthly, method, employerName, employerNif, employmentStartDate, payslipsCount) =
                await ValidateEmployeeAsync(submission, user);
        }
        else
        {
            (averageMonthly, method, employerName, employmentStartDate, payslipsCount) =
                await ValidateSelfEmployedAsync(submission, user);
        }

        var range = await ResolveRangeAsync(averageMonthly)
            ?? throw new Exception("Não foi possível mapear o rendimento numa faixa salarial. Contacta o suporte.");

        application.IncomeRangeId = range.Id;
        application.IncomeValidatedAt = DateTime.UtcNow;
        application.EmploymentType = submission.EmploymentType;
        application.IncomeValidationMethod = method;
        application.PayslipsProvidedCount = payslipsCount;
        application.EmployerName = employerName;
        application.EmployerNif = employerNif;
        application.EmploymentStartDate = employmentStartDate;
        if (wasRequestedByLandlord)
            application.Status = ApplicationStatus.InterestConfirmed;
        application.UpdatedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = tenantId,
            Action = "Rendimentos Validados",
            Message = wasRequestedByLandlord
                ? $"Faixa salarial validada via {DescribeMethod(method)}: {range.Label}."
                : $"Inquilino validou rendimentos voluntariamente ({DescribeMethod(method)}). Faixa: {range.Label}.",
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
            "Income validation OK for application {ApplicationId}: range {RangeCode}, method {Method}",
            application.Id, range.Code, method);

        return new IncomeValidationResultDto(
            application.Id,
            range.Id,
            range.Code,
            range.Label,
            application.IncomeValidatedAt!.Value,
            method.ToString(),
            submission.EmploymentType.ToString(),
            payslipsCount,
            employerName,
            employerNif
        );
    }

    private async Task<(decimal Average, IncomeValidationMethod Method, string? EmployerName, string? EmployerNif, DateTime? StartDate, int PayslipsCount)>
        ValidateEmployeeAsync(IncomeValidationSubmissionDto submission, User user)
    {
        var payslips = submission.Payslips ?? Array.Empty<(Stream, string)>();
        if (payslips.Count == 0)
            throw new ArgumentException("Tens de enviar pelo menos 1 recibo de vencimento.");
        if (payslips.Count > MaxPayslips)
            throw new ArgumentException($"Aceitamos no máximo {MaxPayslips} recibos.");

        var extracted = new List<ReciboVencimentoResponse>(payslips.Count);
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
                _logger.LogWarning(ex, "Falha a extrair recibo {Index}", i + 1);
                throw new Exception($"[{label}] {ex.Message}");
            }

            GeminiResponseValidator.EnsureValid(response, label);

            var currency = (response.Currency ?? "EUR").Trim().ToUpperInvariant();
            if (currency != "EUR" && currency != "€")
                throw new Exception($"[{label}] Moeda não suportada ({response.Currency}). Aceitamos apenas recibos em euros.");

            var nif = NormalizeNif(response.EmployeeNif);
            if (nif != user.Nif)
                throw new Exception($"[{label}] O NIF do recibo ({MaskNif(nif)}) não corresponde ao teu NIF.");

            if (!NameMatcher.IsLikelySame(response.EmployeeName, user.Name))
                throw new Exception($"[{label}] O nome do recibo não corresponde ao teu nome.");

            if (!response.NetSalary.HasValue || response.NetSalary.Value <= 0)
                throw new Exception($"[{label}] Não foi possível ler o vencimento líquido. Tenta com o PDF original.");

            extracted.Add(response);
        }

        var months = extracted.Select(r => ParseReferenceMonth(r.ReferenceMonth)).ToList();
        if (months.Any(m => m == null))
            throw new Exception("Pelo menos um recibo não tem mês de referência legível.");

        var distinctMonths = months.Select(m => m!.Value).Distinct().ToList();
        if (distinctMonths.Count != extracted.Count)
            throw new Exception("Os recibos têm de ser de meses diferentes.");

        var oldestAllowed = FirstDayUtc(DateTime.UtcNow).AddMonths(-MaxAgeMonths);
        if (distinctMonths.Min() < oldestAllowed)
            throw new Exception($"Aceitamos apenas recibos dos últimos {MaxAgeMonths} meses.");

        // Empregador: nome + NIF do primeiro recibo, validado contra os outros.
        var employerNif = NormalizeNif(extracted[0].EmployerNif);
        var employerName = extracted[0].EmployerName?.Trim();
        for (int i = 1; i < extracted.Count; i++)
        {
            var otherNif = NormalizeNif(extracted[i].EmployerNif);
            if (!string.IsNullOrEmpty(employerNif) && !string.IsNullOrEmpty(otherNif) && employerNif != otherNif)
                throw new Exception($"O NIF do empregador difere entre os recibos ({MaskNif(employerNif)} vs {MaskNif(otherNif)}). Não aceitamos múltiplos empregadores.");
        }

        var avg = extracted.Average(r => r.NetSalary!.Value);

        IncomeValidationMethod method;
        DateTime? startDate = null;

        if (extracted.Count >= MinPayslipsToSkipDeclaration)
        {
            method = IncomeValidationMethod.Payslips;
        }
        else
        {
            // <3 recibos: declaração de empregador é obrigatória.
            if (submission.EmployerDeclaration is null)
                throw new ArgumentException(
                    $"Enviaste {extracted.Count} recibo(s). Para validar com menos de {MinPayslipsToSkipDeclaration} tens de juntar uma declaração da entidade empregadora.");

            var (declStream, declName) = submission.EmployerDeclaration.Value;
            DeclaracaoEntidadeEmpregadoraResponse decl;
            try
            {
                decl = await _geminiService.ExtractDocumentAsync<DeclaracaoEntidadeEmpregadoraResponse>(
                    declStream, declName, DocumentPrompts.DeclaracaoEntidadeEmpregadora);
            }
            catch (Exception ex)
            {
                throw new Exception($"[Declaração] {ex.Message}");
            }
            GeminiResponseValidator.EnsureValid(decl, "Declaração de empregador");

            var declEmpNif = NormalizeNif(decl.EmployeeNif);
            if (!string.IsNullOrEmpty(declEmpNif) && declEmpNif != user.Nif)
                throw new Exception("[Declaração] O NIF do trabalhador na declaração não corresponde ao teu.");
            if (!string.IsNullOrWhiteSpace(decl.EmployeeName)
                && !NameMatcher.IsLikelySame(decl.EmployeeName, user.Name))
                throw new Exception("[Declaração] O nome do trabalhador na declaração não corresponde ao teu.");

            var declEmployerNif = NormalizeNif(decl.EmployerNif);
            if (!string.IsNullOrEmpty(employerNif) && !string.IsNullOrEmpty(declEmployerNif)
                && declEmployerNif != employerNif)
                throw new Exception("[Declaração] O NIF do empregador na declaração não corresponde ao dos recibos.");

            if (!string.IsNullOrWhiteSpace(employerName) && !string.IsNullOrWhiteSpace(decl.EmployerName)
                && !NameMatcher.IsLikelySame(decl.EmployerName, employerName))
                throw new Exception("[Declaração] O nome do empregador na declaração não corresponde ao dos recibos.");

            if (decl.HasSignatureAndStamp == false)
                throw new Exception("[Declaração] A declaração tem de estar assinada E carimbada pela entidade empregadora.");

            var declIssue = ParseDate(decl.IssueDate);
            if (declIssue == null)
                throw new Exception("[Declaração] Não foi possível ler a data de emissão.");
            if ((DateTime.UtcNow - declIssue.Value).TotalDays > DeclarationMaxAgeDays)
                throw new Exception($"[Declaração] A declaração tem de ter sido emitida nos últimos {DeclarationMaxAgeDays} dias.");

            startDate = ParseDate(decl.EmploymentStartDate);

            employerName ??= decl.EmployerName?.Trim();
            if (string.IsNullOrEmpty(employerNif)) employerNif = declEmployerNif;

            method = IncomeValidationMethod.PayslipsWithEmployerDeclaration;
        }

        return (avg, method, employerName, employerNif, startDate, extracted.Count);
    }

    private async Task<(decimal Average, IncomeValidationMethod Method, string? ActivityName, DateTime? StartDate, int ReceiptsCount)>
        ValidateSelfEmployedAsync(IncomeValidationSubmissionDto submission, User user)
    {
        if (submission.ActivityDeclaration is null)
            throw new ArgumentException("Tens de enviar a declaração de atividade do Portal das Finanças.");

        var (actStream, actName) = submission.ActivityDeclaration.Value;
        DeclaracaoInicioAtividadeResponse activity;
        try
        {
            activity = await _geminiService.ExtractDocumentAsync<DeclaracaoInicioAtividadeResponse>(
                actStream, actName, DocumentPrompts.DeclaracaoInicioAtividade);
        }
        catch (Exception ex)
        {
            throw new Exception($"[Declaração de atividade] {ex.Message}");
        }
        GeminiResponseValidator.EnsureValid(activity, "Declaração de atividade");

        var actNif = NormalizeNif(activity.TaxpayerNif);
        if (actNif != user.Nif)
            throw new Exception("[Declaração de atividade] O NIF do contribuinte não corresponde ao teu.");

        if (!string.IsNullOrWhiteSpace(activity.TaxpayerName)
            && !NameMatcher.IsLikelySame(activity.TaxpayerName, user.Name))
            throw new Exception("[Declaração de atividade] O nome do contribuinte não corresponde ao teu.");

        if (!string.IsNullOrWhiteSpace(activity.ActivityStatus)
            && !activity.ActivityStatus.Trim().StartsWith("Activ", StringComparison.OrdinalIgnoreCase))
            throw new Exception($"[Declaração de atividade] A atividade não está activa (estado: {activity.ActivityStatus}).");

        var startDate = ParseDate(activity.ActivityStartDate);

        var activityLabel = !string.IsNullOrWhiteSpace(activity.CaePrincipalDescription)
            ? activity.CaePrincipalDescription!.Trim()
            : (activity.CaeCodes?.FirstOrDefault() is { } cae ? $"CAE {cae}" : "Trabalhador independente");

        var receipts = submission.Payslips ?? Array.Empty<(Stream, string)>();
        if (receipts.Count == 0)
            throw new ArgumentException("Tens de enviar pelo menos 1 recibo verde para calcular o rendimento.");
        if (receipts.Count > MaxPayslips)
            throw new ArgumentException($"Aceitamos no máximo {MaxPayslips} recibos verdes.");

        var extracted = new List<ReciboVerdeResponse>(receipts.Count);
        for (int i = 0; i < receipts.Count; i++)
        {
            var (stream, fileName) = receipts[i];
            var label = $"Recibo Verde {i + 1}";
            ReciboVerdeResponse response;
            try
            {
                response = await _geminiService.ExtractDocumentAsync<ReciboVerdeResponse>(
                    stream, fileName, DocumentPrompts.ReciboVerde);
            }
            catch (Exception ex)
            {
                throw new Exception($"[{label}] {ex.Message}");
            }
            GeminiResponseValidator.EnsureValid(response, label);

            var currency = (response.Currency ?? "EUR").Trim().ToUpperInvariant();
            if (currency != "EUR" && currency != "€")
                throw new Exception($"[{label}] Moeda não suportada ({response.Currency}).");

            var nif = NormalizeNif(response.IssuerNif);
            if (nif != user.Nif)
                throw new Exception($"[{label}] O NIF do prestador ({MaskNif(nif)}) não corresponde ao teu NIF.");

            if (!NameMatcher.IsLikelySame(response.IssuerName, user.Name))
                throw new Exception($"[{label}] O nome do prestador não corresponde ao teu nome.");

            if (!response.BaseAmount.HasValue || response.BaseAmount.Value <= 0)
                throw new Exception($"[{label}] Não foi possível ler o valor base.");

            extracted.Add(response);
        }

        var months = extracted.Select(r =>
            ParseReferenceMonth(r.ReferenceMonth)
            ?? (ParseDate(r.IssueDate) is { } d ? new DateTime(d.Year, d.Month, 1, 0, 0, 0, DateTimeKind.Utc) : (DateTime?)null)
        ).ToList();

        if (months.Any(m => m == null))
            throw new Exception("Pelo menos um recibo verde não tem mês legível.");

        var distinctMonths = months.Select(m => m!.Value).Distinct().ToList();
        if (distinctMonths.Count != extracted.Count)
            throw new Exception("Os recibos verdes têm de ser de meses diferentes.");

        var oldestAllowed = FirstDayUtc(DateTime.UtcNow).AddMonths(-MaxAgeMonths);
        if (distinctMonths.Min() < oldestAllowed)
            throw new Exception($"Aceitamos apenas recibos verdes dos últimos {MaxAgeMonths} meses.");

        var avg = extracted.Average(r => r.BaseAmount!.Value);

        return (avg, IncomeValidationMethod.ActivityWithGreenReceipts, activityLabel, startDate, extracted.Count);
    }

    public async Task<IncomeValidationResultDto> SimulateValidationAsync(Guid applicationId, Guid tenantId, string? scenario = null)
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

        // Configura cenário simulado
        EmploymentType empType;
        IncomeValidationMethod method;
        int payslipsCount;
        string employerName;
        string? employerNif;
        switch ((scenario ?? "employee").ToLowerInvariant())
        {
            case "employee-declaration":
            case "employee-with-declaration":
                empType = EmploymentType.Employee;
                method = IncomeValidationMethod.PayslipsWithEmployerDeclaration;
                payslipsCount = 1;
                employerName = "Empregador SIM (DEV)";
                employerNif = "500000000";
                break;
            case "self-employed":
            case "selfemployed":
            case "independent":
                empType = EmploymentType.SelfEmployed;
                method = IncomeValidationMethod.ActivityWithGreenReceipts;
                payslipsCount = 2;
                employerName = "Programação informática (CAE 62010) [DEV]";
                employerNif = null;
                break;
            case "employee":
            default:
                empType = EmploymentType.Employee;
                method = IncomeValidationMethod.Payslips;
                payslipsCount = 3;
                employerName = "Empregador SIM (DEV)";
                employerNif = "500000000";
                break;
        }

        application.IncomeRangeId = range.Id;
        application.IncomeValidatedAt = DateTime.UtcNow;
        application.EmploymentType = empType;
        application.IncomeValidationMethod = method;
        application.PayslipsProvidedCount = payslipsCount;
        application.EmployerName = employerName;
        application.EmployerNif = employerNif;
        application.EmploymentStartDate = DateTime.UtcNow.AddYears(-2);
        if (wasRequestedByLandlord)
            application.Status = ApplicationStatus.InterestConfirmed;
        application.UpdatedAt = DateTime.UtcNow;

        _context.ApplicationHistories.Add(new ApplicationHistory
        {
            ApplicationId = application.Id,
            ActorId = tenantId,
            Action = "Rendimentos Validados (DEV)",
            Message = $"[DEV] Validação simulada ({DescribeMethod(method)}) — faixa {range.Label}.",
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

        return new IncomeValidationResultDto(
            application.Id,
            range.Id,
            range.Code,
            range.Label,
            application.IncomeValidatedAt!.Value,
            method.ToString(),
            empType.ToString(),
            payslipsCount,
            application.EmployerName,
            application.EmployerNif
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

    private static string DescribeMethod(IncomeValidationMethod method) => method switch
    {
        IncomeValidationMethod.Payslips => "3 recibos",
        IncomeValidationMethod.PayslipsWithEmployerDeclaration => "recibos + declaração do empregador",
        IncomeValidationMethod.ActivityWithGreenReceipts => "declaração de atividade + recibos verdes",
        _ => method.ToString()
    };

    private static string NormalizeNif(string? value)
        => (value ?? string.Empty).Replace(" ", "").Replace("-", "").Replace(".", "").Trim();

    private static DateTime FirstDayUtc(DateTime utcNow)
        => new(utcNow.Year, utcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

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

    public static DateTime? ParseDate(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var formats = new[] { "dd/MM/yyyy", "d/M/yyyy", "yyyy-MM-dd", "dd-MM-yyyy" };
        if (DateTime.TryParseExact(input.Trim(), formats, CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var parsed))
            return DateTime.SpecifyKind(parsed.Date, DateTimeKind.Utc);
        return null;
    }

    private static string MaskNif(string nif)
    {
        if (string.IsNullOrWhiteSpace(nif) || nif.Length < 4) return "***";
        return new string('*', nif.Length - 3) + nif[^3..];
    }
}
