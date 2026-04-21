namespace TrustRent.Modules.Catalog.Models.ReferenceData;

public class Typology
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: T0, T1, STUDIO
    public string Name { get; set; } = string.Empty; // display (ex: T0, Studio)
    public int Bedrooms { get; set; } // 0 para Studio/T0
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
