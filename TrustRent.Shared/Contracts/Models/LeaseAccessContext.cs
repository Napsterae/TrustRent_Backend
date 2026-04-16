namespace TrustRent.Shared.Contracts.Models;

public class LeaseAccessContext
{
    public Guid LeaseId { get; set; }
    public Guid TenantId { get; set; }
    public Guid LandlordId { get; set; }
    public Guid PropertyId { get; set; }

    // Dados financeiros para cálculo de pagamentos
    public decimal MonthlyRent { get; set; }
    public decimal? Deposit { get; set; }
    public int AdvanceRentMonths { get; set; }
    public string? LeaseStatus { get; set; }
}
