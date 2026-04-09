namespace TrustRent.Modules.Catalog.Models;

public class PropertyPeriodicity
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public int DurationMonths { get; set; }
}
