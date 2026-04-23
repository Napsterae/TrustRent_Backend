namespace TrustRent.Modules.Catalog.Models.ReferenceData;

public class SalaryRange
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: LT_1000, R_1000_2000
    public string Label { get; set; } = string.Empty; // ex: "< 1.000 €", "1.000 – 2.000 €"
    public decimal? MinAmount { get; set; } // inclusive; null = sem limite inferior
    public decimal? MaxAmount { get; set; } // exclusive; null = sem limite superior
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
