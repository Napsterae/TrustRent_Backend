namespace TrustRent.Modules.Catalog.Models.ReferenceData;

public class Parish
{
    public Guid Id { get; set; }
    public Guid MunicipalityId { get; set; }
    public string Code { get; set; } = string.Empty; // slug único dentro do município
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Municipality? Municipality { get; set; }
}
