namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Pedido de atualização de renda pelo senhorio, conforme os coeficientes anuais
/// publicados pelo INE (Art. 1077.º do Código Civil e Art. 24.º do NRAU).
/// </summary>
public class RentIncreaseRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeaseId { get; set; }
    public Guid RequestedById { get; set; }

    // Valores
    public decimal CurrentRent { get; set; }
    public decimal ProposedRent { get; set; }
    public decimal IncreasePercentage { get; set; }
    public decimal CoefficientApplied { get; set; }
    public int CoefficientYear { get; set; }

    /// <summary>Se acumulou coeficientes de anos anteriores (máx. 3 anos sem aumento).</summary>
    public bool AccumulatedCoefficients { get; set; } = false;
    public string? AccumulatedDetails { get; set; }

    // Datas
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;
    /// <summary>Data a partir da qual o aumento entra em vigor (mín. 30 dias após comunicação).</summary>
    public DateTime EffectiveDate { get; set; }
    /// <summary>Data limite para o inquilino contestar (30 dias após comunicação).</summary>
    public DateTime ContestationDeadline { get; set; }

    // Estado: Pending (aguarda 30 dias), Contested, Effective, Cancelled
    public string Status { get; set; } = "Pending";

    // Contestação pelo inquilino
    public bool Contested { get; set; } = false;
    public DateTime? ContestedAt { get; set; }
    public string? ContestationReason { get; set; }
    public string? ContestationResolution { get; set; }

    // Processamento
    public DateTime? ProcessedAt { get; set; }
    public bool Applied { get; set; } = false;

    // Registo legal
    public string? RequesterIpAddress { get; set; }
    public string? RequesterUserAgent { get; set; }

    /// <summary>
    /// Coeficientes de atualização de rendas publicados pelo INE (Portaria anual).
    /// Dados históricos de 2016 a 2026.
    /// </summary>
    public static readonly Dictionary<int, decimal> YearlyCoefficients = new()
    {
        { 2016, 1.0016m },
        { 2017, 1.0054m },
        { 2018, 1.0112m },
        { 2019, 1.0115m },
        { 2020, 1.0051m },
        { 2021, 0.9997m },
        { 2022, 1.0043m },
        { 2023, 1.0200m },
        { 2024, 1.0694m },
        { 2025, 1.0216m },
        { 2026, 1.0224m },
    };

    /// <summary>
    /// Obtém o coeficiente para o ano atual. Se não existir, retorna 1 (sem aumento).
    /// </summary>
    public static decimal GetCurrentCoefficient()
    {
        return YearlyCoefficients.TryGetValue(DateTime.UtcNow.Year, out var coeff) ? coeff : 1.0m;
    }

    /// <summary>
    /// Calcula o coeficiente acumulado para os últimos N anos sem aumento (máx. 3).
    /// </summary>
    public static (decimal coefficient, string details) GetAccumulatedCoefficient(int lastIncreaseYear)
    {
        var currentYear = DateTime.UtcNow.Year;
        var yearsWithoutIncrease = currentYear - lastIncreaseYear;

        if (yearsWithoutIncrease <= 1)
        {
            var coeff = GetCurrentCoefficient();
            return (coeff, $"Coeficiente {currentYear}: {coeff}");
        }

        // Acumular até 3 anos
        var maxYears = Math.Min(yearsWithoutIncrease, 3);
        var accumulated = 1.0m;
        var details = new List<string>();

        for (var i = maxYears - 1; i >= 0; i--)
        {
            var year = currentYear - i;
            if (YearlyCoefficients.TryGetValue(year, out var coeff))
            {
                accumulated *= coeff;
                details.Add($"{year}: {coeff}");
            }
        }

        return (accumulated, $"Coeficientes acumulados ({string.Join(" × ", details)}) = {accumulated:F6}");
    }

    /// <summary>
    /// Calcula a nova renda arredondada para o cêntimo superior (Art. 1077.º CC).
    /// </summary>
    public static decimal CalculateNewRent(decimal currentRent, decimal coefficient)
    {
        var newRent = currentRent * coefficient;
        // Arredondamento para o cêntimo imediatamente superior
        return Math.Ceiling(newRent * 100) / 100;
    }
}
