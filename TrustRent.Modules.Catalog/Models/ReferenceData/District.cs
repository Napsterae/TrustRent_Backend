namespace TrustRent.Modules.Catalog.Models.ReferenceData;

public class District
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty; // natural key (slug)
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemDefault { get; set; } = true; // criado pelo seeder; não é sobrescrito depois
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public List<Municipality> Municipalities { get; set; } = new();
}
