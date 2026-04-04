using System.ComponentModel.DataAnnotations;

namespace TrustRent.Modules.Communications.Models;

public enum MessageContextType
{
    Application,
    Ticket
}

public class Message
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ContextId { get; set; }

    [Required]
    public MessageContextType ContextType { get; set; }

    [Required]
    public Guid SenderId { get; set; }

    [Required]
    public string Content { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
