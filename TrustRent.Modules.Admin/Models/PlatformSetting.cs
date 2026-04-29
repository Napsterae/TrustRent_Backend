namespace TrustRent.Modules.Admin.Models;

public class PlatformSetting
{
    public string Key { get; set; } = string.Empty;
    public string? Value { get; set; }
    public string? Category { get; set; }
    public string? Description { get; set; }
    public string ValueType { get; set; } = "string"; // string|number|bool|json
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByAdminId { get; set; }
}
