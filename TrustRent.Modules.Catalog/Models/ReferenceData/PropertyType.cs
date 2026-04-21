namespace TrustRent.Modules.Catalog.Models.ReferenceData;

public class PropertyType
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty; // ex: APARTMENT, HOUSE, ROOM
    public string Name { get; set; } = string.Empty; // display (ex: Apartamento)
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
