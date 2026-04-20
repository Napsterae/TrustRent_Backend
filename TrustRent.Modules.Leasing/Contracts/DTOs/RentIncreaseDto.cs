namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public class RentIncreaseInfoDto
{
    public Guid LeaseId { get; set; }
    public decimal CurrentRent { get; set; }
    public decimal CurrentCoefficient { get; set; }
    public int CoefficientYear { get; set; }
    public decimal ProposedRent { get; set; }
    public decimal IncreasePercentage { get; set; }
    public decimal IncreaseAmount { get; set; }
    public bool CanIncreaseNow { get; set; }
    public string? CannotIncreaseReason { get; set; }
    public DateTime? EarliestIncreaseDate { get; set; }

    // Acumulação
    public bool CanAccumulate { get; set; }
    public decimal? AccumulatedCoefficient { get; set; }
    public decimal? AccumulatedProposedRent { get; set; }
    public decimal? AccumulatedIncreasePercentage { get; set; }
    public string? AccumulatedDetails { get; set; }

    // Pedidos existentes
    public bool HasPendingRequest { get; set; }
    public DateTime? LastIncreaseDate { get; set; }
}

public class RequestRentIncreaseDto
{
    /// <summary>Se true, aplica coeficientes acumulados dos últimos 3 anos sem aumento.</summary>
    public bool UseAccumulated { get; set; } = false;
}

public class ContestRentIncreaseDto
{
    public string Reason { get; set; } = string.Empty;
}

public class RentIncreaseResultDto
{
    public Guid RequestId { get; set; }
    public decimal CurrentRent { get; set; }
    public decimal NewRent { get; set; }
    public decimal IncreasePercentage { get; set; }
    public decimal CoefficientApplied { get; set; }
    public DateTime EffectiveDate { get; set; }
    public DateTime ContestationDeadline { get; set; }
    public string Status { get; set; } = string.Empty;
}
