namespace TrustRent.Modules.Leasing.Models;

/// <summary>
/// Registo legal e imutável de comunicações oficiais no âmbito do arrendamento.
/// Guarda data/hora, IP de envio e de visualização para efeitos probatórios
/// conforme artigo 9.º e 10.º do NRAU (Lei n.º 6/2006, atualizada) e
/// artigo 224.º do Código Civil.
/// </summary>
public class LegalCommunicationLog
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid LeaseId { get; set; }

    /// <summary>Tipo de comunicação: RenewalNotification, NonRenewalResponse, CancelNotification, etc.</summary>
    public string CommunicationType { get; set; } = string.Empty;

    /// <summary>ID do utilizador remetente (quem originou a comunicação).</summary>
    public Guid SenderId { get; set; }

    /// <summary>ID do utilizador destinatário.</summary>
    public Guid RecipientId { get; set; }

    /// <summary>Conteúdo da comunicação.</summary>
    public string Content { get; set; } = string.Empty;

    // ── Dados de Envio ──
    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public string SenderIpAddress { get; set; } = string.Empty;
    public string? SenderUserAgent { get; set; }

    // ── Dados de Visualização ──
    public DateTime? ViewedAt { get; set; }
    public string? ViewerIpAddress { get; set; }
    public string? ViewerUserAgent { get; set; }

    // ── Dados de Confirmação de Receção ──
    public DateTime? AcknowledgedAt { get; set; }
    public string? AcknowledgerIpAddress { get; set; }

    /// <summary>ID da notificação na plataforma (link à tabela Notifications).</summary>
    public Guid? NotificationId { get; set; }

    /// <summary>ID da renovação associada, se aplicável.</summary>
    public Guid? RenewalNotificationId { get; set; }

    /// <summary>Se foi também enviado por email.</summary>
    public bool EmailSent { get; set; } = false;
    public DateTime? EmailSentAt { get; set; }
    public string? EmailRecipientAddress { get; set; }

    /// <summary>
    /// Hash SHA-256 do conteúdo da comunicação no momento do envio,
    /// para provar que o conteúdo não foi alterado.
    /// </summary>
    public string? ContentHash { get; set; }
}
