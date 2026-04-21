namespace TrustRent.Modules.Identity.Models;

public class PhoneCountry
{
    public Guid Id { get; set; }
    public string IsoCode { get; set; } = string.Empty; // PT, ES, FR...
    public string Name { get; set; } = string.Empty;
    public string DialCode { get; set; } = string.Empty; // +351
    public string MobilePattern { get; set; } = string.Empty; // regex validator (sem delimitadores /.../)
    public string Example { get; set; } = string.Empty;
    public string FlagEmoji { get; set; } = string.Empty;
    public int DisplayOrder { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsDefault { get; set; }
    public bool IsSystemDefault { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
