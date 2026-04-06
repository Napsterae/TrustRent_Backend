using System.ComponentModel.DataAnnotations;

namespace TrustRent.Modules.Communications.Models;

public class Notification
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    
    [Required]
    public Guid UserId { get; set; }           // Destinatário
    
    [Required]
    [MaxLength(50)]
    public string Type { get; set; } = string.Empty; // "application", "ticket", "payment", "property"
    
    [Required]
    [MaxLength(500)]
    public string Message { get; set; } = string.Empty;
    
    public bool IsRead { get; set; } = false;
    
    public Guid? ReferenceId { get; set; }      // ID do recurso relacionado (PropertyId, ApplicationId, TicketId)
    
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
