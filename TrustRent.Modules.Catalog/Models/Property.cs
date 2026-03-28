namespace TrustRent.Modules.Catalog.Models;

public class Property
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Typology { get; set; } = string.Empty;
}

