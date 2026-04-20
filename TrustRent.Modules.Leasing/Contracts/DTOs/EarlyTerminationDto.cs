namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public class RequestEarlyTerminationDto
{
    public string Reason { get; set; } = string.Empty;
    public DateTime ProposedTerminationDate { get; set; }
}

public class EarlyTerminationInfoDto
{
    public Guid LeaseId { get; set; }
    public DateTime LeaseStartDate { get; set; }
    public DateTime LeaseEndDate { get; set; }
    public int DurationMonths { get; set; }
    public DateTime OneThirdDate { get; set; }
    public bool CanTerminateNow { get; set; }
    public int RequiredNoticeDays { get; set; }
    public DateTime EarliestTerminationDate { get; set; }
    public decimal MonthlyRent { get; set; }
    public decimal? PotentialIndemnification { get; set; }
    public string? IndemnificationReason { get; set; }
    public bool HasPendingRequest { get; set; }
}

public class EarlyTerminationResultDto
{
    public Guid TerminationRequestId { get; set; }
    public DateTime ProposedTerminationDate { get; set; }
    public DateTime EarliestTerminationDate { get; set; }
    public int RequiredNoticeDays { get; set; }
    public decimal? IndemnificationAmount { get; set; }
    public bool IndemnificationRequired { get; set; }
    public string? IndemnificationReason { get; set; }
    public string Status { get; set; } = string.Empty;
}
