namespace TrustRent.Modules.Admin.Models;

public class FeatureFlag
{
    public string Key { get; set; } = string.Empty;
    public bool Enabled { get; set; } = false;
    public int RolloutPercent { get; set; } = 0;
    public string? Description { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public Guid? UpdatedByAdminId { get; set; }
}
