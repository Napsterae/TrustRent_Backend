namespace TrustRent.Modules.Leasing.Models;

public class LeaseRenewalNotification
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    
    public DateTime NotifiedAt { get; set; } = DateTime.UtcNow;
    public DateTime DeadlineDate { get; set; }
    
    public string? LandlordResponse { get; set; } // "Renew" or "Cancel"
    public DateTime? LandlordRespondedAt { get; set; }
    public string? LandlordResponseIpAddress { get; set; }
    
    public string? TenantResponse { get; set; } // "Renew" or "Cancel"
    public DateTime? TenantRespondedAt { get; set; }
    public string? TenantResponseIpAddress { get; set; }
    
    public bool Processed { get; set; } = false;

    /// <summary>Prazo legal de oposição do senhorio em dias (Art. 1097.º CC).</summary>
    public int LandlordNoticeDays { get; set; }

    /// <summary>Prazo legal de oposição do inquilino em dias (Art. 1098.º CC).</summary>
    public int TenantNoticeDays { get; set; }
}
