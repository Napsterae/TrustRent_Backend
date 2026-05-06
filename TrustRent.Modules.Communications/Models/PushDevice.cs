using System.ComponentModel.DataAnnotations;

namespace TrustRent.Modules.Communications.Models;

public class PushDevice
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid UserId { get; set; }

    [Required]
    [MaxLength(255)]
    public string ExpoPushToken { get; set; } = string.Empty;

    [Required]
    [MaxLength(20)]
    public string Platform { get; set; } = string.Empty;

    [MaxLength(120)]
    public string? DeviceName { get; set; }

    [MaxLength(40)]
    public string? AppVersion { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}