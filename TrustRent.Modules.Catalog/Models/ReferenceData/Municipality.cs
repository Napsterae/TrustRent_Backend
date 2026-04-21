namespace TrustRent.Modules.Catalog.Models.ReferenceData;

public class Municipality
{
    public Guid Id { get; set; }
    public Guid DistrictId { get; set; }
    public string Code { get; set; } = string.Empty; // slug único dentro do distrito
    public string Name { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsSystemDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public District? District { get; set; }
    public List<Parish> Parishes { get; set; } = new();
}
