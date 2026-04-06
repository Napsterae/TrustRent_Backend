namespace TrustRent.Modules.Catalog.Models;

public class Amenity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string IconName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}
