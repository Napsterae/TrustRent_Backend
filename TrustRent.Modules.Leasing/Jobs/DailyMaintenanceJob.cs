using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Jobs;

public interface IDailyMaintenanceJob
{
    Task ExecuteAsync();
}

public class DailyMaintenanceJob : IDailyMaintenanceJob
{
    private readonly LeasingDbContext _db;
    private readonly IReviewService _reviewService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<DailyMaintenanceJob> _logger;

    public DailyMaintenanceJob(
        LeasingDbContext db,
        IReviewService reviewService,
        INotificationService notificationService,
        ILogger<DailyMaintenanceJob> logger)
    {
        _db = db;
        _reviewService = reviewService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task ExecuteAsync()
    {
        _logger.LogInformation("Daily maintenance job started at {Time}", DateTime.UtcNow);

        await ProcessLeaseExpirations();
        await ProcessLeaseRenewalNotifications();
        await ProcessQuarterlyLeaseReviews();
        await ProcessClosedTicketReviews();
        await _reviewService.ProcessExpiredReviewsAsync();
        await ProcessRenewalDecisions();
        await ProcessTerminationRequests();
        await ProcessRentIncreases();
        await ProcessTaxRegistrationAlerts();

        _logger.LogInformation("Daily maintenance job completed at {Time}", DateTime.UtcNow);
    }

    /// <summary>
    /// Expire leases that have passed their end date.
    /// </summary>
    private async Task ProcessLeaseExpirations()
    {
        var expiredLeases = await _db.Leases
            .Where(l => l.Status == LeaseStatus.Active && l.EndDate <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var lease in expiredLeases)
        {
            lease.Status = LeaseStatus.Expired;
            lease.UpdatedAt = DateTime.UtcNow;

            await _notificationService.SendNotificationAsync(
                lease.LandlordId, "LeaseExpired",
                "O seu contrato de arrendamento expirou.", lease.Id);
            await _notificationService.SendNotificationAsync(
                lease.TenantId, "LeaseExpired",
                "O seu contrato de arrendamento expirou.", lease.Id);

            _logger.LogInformation("Lease {LeaseId} expired", lease.Id);
        }

        if (expiredLeases.Count > 0)
            await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Check active leases approaching their end date and send renewal notifications
    /// based on Portuguese law deadlines.
    /// </summary>
    private async Task ProcessLeaseRenewalNotifications()
    {
        var activeLeases = await _db.Leases
            .Where(l => l.Status == LeaseStatus.Active && l.AllowsRenewal)
            .ToListAsync();

        foreach (var lease in activeLeases)
        {
            // Check if we already sent a notification for this lease's current period
            var existingNotification = await _db.LeaseRenewalNotifications
                .AnyAsync(n => n.LeaseId == lease.Id && !n.Processed);

            if (existingNotification) continue;

            var legalDeadlineDays = GetLegalNotificationDays(lease.DurationMonths);
            var notificationDate = lease.EndDate.AddDays(-(legalDeadlineDays + 14)); // +14 days margin

            if (DateTime.UtcNow >= notificationDate && DateTime.UtcNow < lease.EndDate)
            {
                var notification = new LeaseRenewalNotification
                {
                    Id = Guid.NewGuid(),
                    LeaseId = lease.Id,
                    DeadlineDate = lease.EndDate.AddDays(-legalDeadlineDays),
                    LandlordNoticeDays = GetLandlordNoticeDays(lease.DurationMonths),
                    TenantNoticeDays = GetTenantNoticeDays(lease.DurationMonths)
                };

                _db.LeaseRenewalNotifications.Add(notification);

                var landlordMessage = $"O seu contrato de arrendamento termina em {(lease.EndDate - DateTime.UtcNow).Days} dias. Deseja renovar ou cancelar?";
                var tenantMessage = landlordMessage;

                await _notificationService.SendNotificationAsync(
                    lease.LandlordId, "LeaseRenewal", landlordMessage, lease.Id);

                await _notificationService.SendNotificationAsync(
                    lease.TenantId, "LeaseRenewal", tenantMessage, lease.Id);

                // Registar comunicações legais (sistema automático — IP "system")
                _db.LegalCommunicationLogs.Add(new LegalCommunicationLog
                {
                    LeaseId = lease.Id,
                    CommunicationType = "RenewalNotification",
                    SenderId = Guid.Empty, // Sistema
                    RecipientId = lease.LandlordId,
                    Content = landlordMessage,
                    SentAt = DateTime.UtcNow,
                    SenderIpAddress = "system",
                    RenewalNotificationId = notification.Id,
                    ContentHash = ComputeSha256(landlordMessage)
                });
                _db.LegalCommunicationLogs.Add(new LegalCommunicationLog
                {
                    LeaseId = lease.Id,
                    CommunicationType = "RenewalNotification",
                    SenderId = Guid.Empty,
                    RecipientId = lease.TenantId,
                    Content = tenantMessage,
                    SentAt = DateTime.UtcNow,
                    SenderIpAddress = "system",
                    RenewalNotificationId = notification.Id,
                    ContentHash = ComputeSha256(tenantMessage)
                });

                _logger.LogInformation("Sent renewal notification for lease {LeaseId}, deadline: {Deadline}",
                    lease.Id, notification.DeadlineDate);
            }
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Every 3 months from the lease start date, create review pairs for active leases.
    /// </summary>
    private async Task ProcessQuarterlyLeaseReviews()
    {
        var activeLeases = await _db.Leases
            .Where(l => l.Status == LeaseStatus.Active)
            .ToListAsync();

        foreach (var lease in activeLeases)
        {
            var monthsSinceStart = (int)((DateTime.UtcNow - lease.StartDate).TotalDays / 30.44);
            if (monthsSinceStart < 3) continue;

            // Check if it's time for a quarterly review (every 3 months)
            var quartersPassed = monthsSinceStart / 3;
            var nextReviewDate = lease.StartDate.AddMonths(quartersPassed * 3);

            // Only create if we're within the current quarter window (± 7 days)
            if (Math.Abs((DateTime.UtcNow - nextReviewDate).TotalDays) > 7) continue;

            try
            {
                await _reviewService.CreateLeaseReviewPairAsync(lease.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating quarterly review for lease {LeaseId}", lease.Id);
            }
        }
    }

    /// <summary>
    /// Create review pairs for recently closed tickets that don't have reviews yet.
    /// </summary>
    private async Task ProcessClosedTicketReviews()
    {
        var closedTickets = await _db.Tickets
            .Where(t => t.Status == TicketStatus.Closed)
            .ToListAsync();

        foreach (var ticket in closedTickets)
        {
            try
            {
                await _reviewService.CreateTicketReviewPairAsync(ticket.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating review for ticket {TicketId}", ticket.Id);
            }
        }
    }

    /// <summary>
    /// Process renewal notifications where both parties have responded.
    /// </summary>
    private async Task ProcessRenewalDecisions()
    {
        var pendingDecisions = await _db.LeaseRenewalNotifications
            .Where(n => !n.Processed
                && n.LandlordResponse != null && n.TenantResponse != null)
            .ToListAsync();

        foreach (var notification in pendingDecisions)
        {
            var lease = await _db.Leases.FindAsync(notification.LeaseId);
            if (lease == null) continue;

            if (notification.LandlordResponse == "Renew" && notification.TenantResponse == "Renew")
            {
                // Both want to renew — extend the lease
                var renewalMonths = lease.DurationMonths;

                // Proteção dos 3 anos (Art. 1096.º CC): contratos de habitação permanente
                // com duração < 3 anos devem ser estendidos até perfazer 3 anos totais
                if (lease.LeaseRegime == "PermanentHousing")
                {
                    var totalMonthsSoFar = (int)((lease.EndDate - lease.CreatedAt).TotalDays / 30.44);
                    if (totalMonthsSoFar < 36)
                    {
                        renewalMonths = Math.Max(renewalMonths, 36 - totalMonthsSoFar);
                    }
                }

                lease.StartDate = lease.EndDate;
                lease.EndDate = lease.EndDate.AddMonths(renewalMonths);
                lease.RenewalDate = DateTime.UtcNow;
                lease.UpdatedAt = DateTime.UtcNow;

                await _notificationService.SendNotificationAsync(
                    lease.LandlordId, "LeaseRenewed",
                    $"O contrato foi renovado até {lease.EndDate:dd/MM/yyyy}.", lease.Id);
                await _notificationService.SendNotificationAsync(
                    lease.TenantId, "LeaseRenewed",
                    $"O contrato foi renovado até {lease.EndDate:dd/MM/yyyy}.", lease.Id);

                _logger.LogInformation("Lease {LeaseId} renewed until {EndDate}", lease.Id, lease.EndDate);
            }
            else
            {
                // At least one party wants to cancel
                // Proteção dos 3 anos (Art. 1096.º CC): ignorar cancelamento se < 3 anos em habitação permanente
                var totalMonths = (int)((lease.EndDate - lease.CreatedAt).TotalDays / 30.44);
                if (lease.LeaseRegime == "PermanentHousing" && totalMonths < 36)
                {
                    // Forçar renovação até perfazer 3 anos
                    var renewalMonths = Math.Max(lease.DurationMonths, 36 - totalMonths);
                    lease.StartDate = lease.EndDate;
                    lease.EndDate = lease.EndDate.AddMonths(renewalMonths);
                    lease.RenewalDate = DateTime.UtcNow;
                    lease.UpdatedAt = DateTime.UtcNow;

                    await _notificationService.SendNotificationAsync(
                        lease.LandlordId, "LeaseAutoRenewed",
                        $"O contrato foi renovado automaticamente até {lease.EndDate:dd/MM/yyyy} (proteção de 3 anos, Art. 1096.º CC).", lease.Id);
                    await _notificationService.SendNotificationAsync(
                        lease.TenantId, "LeaseAutoRenewed",
                        $"O contrato foi renovado automaticamente até {lease.EndDate:dd/MM/yyyy} (proteção de 3 anos, Art. 1096.º CC).", lease.Id);

                    _logger.LogInformation("Lease {LeaseId} auto-renewed due to 3-year protection until {EndDate}", lease.Id, lease.EndDate);
                }
                else
                {
                    // lease will expire naturally
                    lease.AllowsRenewal = false;
                    lease.UpdatedAt = DateTime.UtcNow;

                    var cancelledBy = notification.LandlordResponse == "Cancel" ? "senhorio" : "inquilino";

                    await _notificationService.SendNotificationAsync(
                        lease.LandlordId, "LeaseNotRenewed",
                        $"O contrato não será renovado. O {cancelledBy} optou por cancelar.", lease.Id);
                    await _notificationService.SendNotificationAsync(
                        lease.TenantId, "LeaseNotRenewed",
                        $"O contrato não será renovado. O {cancelledBy} optou por cancelar.", lease.Id);

                    _logger.LogInformation("Lease {LeaseId} will not be renewed — {Party} cancelled", lease.Id, cancelledBy);
                }
            }

            notification.Processed = true;
        }

        // Also process overdue notifications where deadline has passed
        var overdueNotifications = await _db.LeaseRenewalNotifications
            .Where(n => !n.Processed && n.DeadlineDate <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var notification in overdueNotifications)
        {
            var lease = await _db.Leases.FindAsync(notification.LeaseId);
            if (lease == null) continue;

            if (notification.LandlordResponse == null && notification.TenantResponse == null)
            {
                // Neither responded — auto-renew if AllowsRenewal
                if (lease.AllowsRenewal)
                {
                    lease.StartDate = lease.EndDate;
                    lease.EndDate = lease.EndDate.AddMonths(lease.DurationMonths);
                    lease.RenewalDate = DateTime.UtcNow;
                    lease.UpdatedAt = DateTime.UtcNow;

                    await _notificationService.SendNotificationAsync(
                        lease.LandlordId, "LeaseAutoRenewed",
                        $"O contrato foi renovado automaticamente até {lease.EndDate:dd/MM/yyyy}.", lease.Id);
                    await _notificationService.SendNotificationAsync(
                        lease.TenantId, "LeaseAutoRenewed",
                        $"O contrato foi renovado automaticamente até {lease.EndDate:dd/MM/yyyy}.", lease.Id);
                }
            }
            else
            {
                // One responded, one didn't — whoever responded with Cancel wins, otherwise auto-renew
                var anyCancel = notification.LandlordResponse == "Cancel" || notification.TenantResponse == "Cancel";
                if (anyCancel)
                {
                    lease.AllowsRenewal = false;
                    lease.UpdatedAt = DateTime.UtcNow;
                }
                else if (lease.AllowsRenewal)
                {
                    lease.StartDate = lease.EndDate;
                    lease.EndDate = lease.EndDate.AddMonths(lease.DurationMonths);
                    lease.RenewalDate = DateTime.UtcNow;
                    lease.UpdatedAt = DateTime.UtcNow;
                }
            }

            notification.Processed = true;
        }

        if (pendingDecisions.Count > 0 || overdueNotifications.Count > 0)
            await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Portuguese law: notification deadlines based on contract duration.
    /// </summary>
    /// <summary>
    /// Prazos de oposição à renovação para o senhorio (Art. 1097.º CC).
    /// </summary>
    private static int GetLandlordNoticeDays(int durationMonths)
    {
        return durationMonths switch
        {
            < 6 => (int)Math.Ceiling(durationMonths * 30.44 / 3),  // 1/3 da duração
            >= 6 and < 12 => 30,    // 6 meses a 1 ano
            >= 12 and < 72 => 60,   // 1 a 6 anos
            >= 72 => 120,           // 6+ anos
        };
    }

    /// <summary>
    /// Prazos de oposição à renovação para o inquilino (Art. 1098.º CC).
    /// </summary>
    private static int GetTenantNoticeDays(int durationMonths)
    {
        return durationMonths switch
        {
            < 6 => (int)Math.Ceiling(durationMonths * 30.44 / 3),  // 1/3 da duração
            >= 6 and < 12 => 60,    // 6 meses a 1 ano
            >= 12 => 120,           // 1+ anos
        };
    }

    /// <summary>
    /// Retorna o MAIOR dos dois prazos (senhorio e inquilino) para garantir que a notificação
    /// é enviada com antecedência suficiente para ambas as partes.
    /// </summary>
    private static int GetLegalNotificationDays(int durationMonths)
    {
        return Math.Max(GetLandlordNoticeDays(durationMonths), GetTenantNoticeDays(durationMonths));
    }

    /// <summary>
    /// Processa pedidos de denúncia antecipada cuja data proposta de término já passou.
    /// Altera o estado do contrato para TerminatedEarly.
    /// </summary>
    private async Task ProcessTerminationRequests()
    {
        var dueRequests = await _db.LeaseTerminationRequests
            .Where(r => r.Status == "Pending" && r.ProposedTerminationDate <= DateTime.UtcNow)
            .ToListAsync();

        foreach (var request in dueRequests)
        {
            var lease = await _db.Leases.FindAsync(request.LeaseId);
            if (lease == null || lease.Status != LeaseStatus.Active)
            {
                request.Status = "Cancelled";
                request.ProcessedAt = DateTime.UtcNow;
                request.ProcessedByNote = "Contrato já não está ativo.";
                continue;
            }

            lease.Status = LeaseStatus.TerminatedEarly;
            lease.UpdatedAt = DateTime.UtcNow;

            request.Status = "Completed";
            request.ProcessedAt = DateTime.UtcNow;
            request.ProcessedByNote = "Denúncia antecipada processada automaticamente na data proposta.";

            var content = $"Denúncia antecipada concluída. O contrato de arrendamento (Lease ID: {lease.Id}) " +
                $"foi terminado antecipadamente em {request.ProposedTerminationDate:dd/MM/yyyy} " +
                $"por pedido do inquilino (Art. 1098.º CC).";

            _db.LegalCommunicationLogs.Add(new LegalCommunicationLog
            {
                LeaseId = lease.Id,
                CommunicationType = "EarlyTerminationCompleted",
                SenderId = Guid.Empty,
                RecipientId = lease.LandlordId,
                Content = content,
                SentAt = DateTime.UtcNow,
                SenderIpAddress = "system",
                ContentHash = ComputeSha256(content)
            });
            _db.LegalCommunicationLogs.Add(new LegalCommunicationLog
            {
                LeaseId = lease.Id,
                CommunicationType = "EarlyTerminationCompleted",
                SenderId = Guid.Empty,
                RecipientId = lease.TenantId,
                Content = content,
                SentAt = DateTime.UtcNow,
                SenderIpAddress = "system",
                ContentHash = ComputeSha256(content)
            });

            await _notificationService.SendNotificationAsync(
                lease.LandlordId, "LeaseTerminatedEarly",
                $"O contrato foi terminado antecipadamente em {request.ProposedTerminationDate:dd/MM/yyyy}.",
                lease.Id);
            await _notificationService.SendNotificationAsync(
                lease.TenantId, "LeaseTerminatedEarly",
                $"O contrato foi terminado antecipadamente em {request.ProposedTerminationDate:dd/MM/yyyy}.",
                lease.Id);

            _logger.LogInformation("Early termination processed for lease {LeaseId}, effective {Date}",
                lease.Id, request.ProposedTerminationDate);
        }

        if (dueRequests.Count > 0)
            await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Processa aumentos de renda cuja data efetiva já passou e não foram contestados.
    /// Aplica a nova renda ao contrato.
    /// </summary>
    private async Task ProcessRentIncreases()
    {
        var dueIncreases = await _db.RentIncreaseRequests
            .Where(r => r.Status == "Pending" && r.EffectiveDate <= DateTime.UtcNow && !r.Contested)
            .ToListAsync();

        foreach (var request in dueIncreases)
        {
            var lease = await _db.Leases.FindAsync(request.LeaseId);
            if (lease == null || lease.Status != LeaseStatus.Active)
            {
                request.Status = "Cancelled";
                request.ProcessedAt = DateTime.UtcNow;
                continue;
            }

            // Aplicar aumento
            var oldRent = lease.MonthlyRent;
            lease.MonthlyRent = request.ProposedRent;
            lease.UpdatedAt = DateTime.UtcNow;

            request.Status = "Effective";
            request.Applied = true;
            request.ProcessedAt = DateTime.UtcNow;

            var content = $"Atualização de renda aplicada automaticamente. " +
                $"Renda anterior: {oldRent:F2}€. Nova renda: {request.ProposedRent:F2}€ " +
                $"(aumento de {request.IncreasePercentage}%, coeficiente {request.CoefficientApplied}).";

            _db.LegalCommunicationLogs.Add(new LegalCommunicationLog
            {
                LeaseId = lease.Id,
                CommunicationType = "RentIncreaseApplied",
                SenderId = Guid.Empty,
                RecipientId = lease.TenantId,
                Content = content,
                SentAt = DateTime.UtcNow,
                SenderIpAddress = "system",
                ContentHash = ComputeSha256(content)
            });
            _db.LegalCommunicationLogs.Add(new LegalCommunicationLog
            {
                LeaseId = lease.Id,
                CommunicationType = "RentIncreaseApplied",
                SenderId = Guid.Empty,
                RecipientId = lease.LandlordId,
                Content = content,
                SentAt = DateTime.UtcNow,
                SenderIpAddress = "system",
                ContentHash = ComputeSha256(content)
            });

            await _notificationService.SendNotificationAsync(
                lease.LandlordId, "RentIncreaseApplied",
                $"A renda foi atualizada de {oldRent:F2}€ para {request.ProposedRent:F2}€.",
                lease.Id);
            await _notificationService.SendNotificationAsync(
                lease.TenantId, "RentIncreaseApplied",
                $"A renda foi atualizada de {oldRent:F2}€ para {request.ProposedRent:F2}€ a partir de hoje.",
                lease.Id);

            _logger.LogInformation("Rent increase applied for lease {LeaseId}: {OldRent}€ → {NewRent}€",
                lease.Id, oldRent, request.ProposedRent);
        }

        if (dueIncreases.Count > 0)
            await _db.SaveChangesAsync();
    }

    private static string ComputeSha256(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    /// <summary>
    /// Alerta senhorios que não registaram o contrato nas Finanças (Autoridade Tributária).
    /// Lei do Arrendamento 2026: obrigação de registo reforçada com penalizações.
    /// Alerta enviado 7, 15 e 30 dias após ativação do contrato.
    /// </summary>
    private async Task ProcessTaxRegistrationAlerts()
    {
        var alertDays = new[] { 7, 15, 30 };
        var unregisteredLeases = await _db.Leases
            .Where(l => l.Status == LeaseStatus.Active && !l.IsRegisteredWithTaxAuthority)
            .ToListAsync();

        foreach (var lease in unregisteredLeases)
        {
            var daysSinceStart = (DateTime.UtcNow - lease.StartDate).Days;

            foreach (var threshold in alertDays)
            {
                if (daysSinceStart != threshold) continue;

                var content = threshold switch
                {
                    7 => "Lembrete: O contrato de arrendamento deve ser registado na Autoridade Tributária. " +
                         "Nos termos da Lei do Arrendamento 2026, o não registo pode resultar em penalizações civis e fiscais.",
                    15 => "⚠️ URGENTE: O contrato ainda não foi registado nas Finanças. " +
                          "O prazo legal para registo é de 30 dias após o início do contrato. Regularize a situação.",
                    30 => "🔴 PRAZO EXPIRADO: Passaram 30 dias sem registo do contrato na Autoridade Tributária. " +
                          "O senhorio pode estar sujeito a coimas e perder benefícios fiscais. Regularize imediatamente.",
                    _ => string.Empty
                };

                if (string.IsNullOrEmpty(content)) continue;

                await _notificationService.SendNotificationAsync(
                    lease.LandlordId, "TaxRegistrationAlert",
                    content, lease.Id);

                _logger.LogInformation("Tax registration alert (day {Days}) sent for lease {LeaseId} to landlord {LandlordId}",
                    threshold, lease.Id, lease.LandlordId);
            }
        }
    }
}
