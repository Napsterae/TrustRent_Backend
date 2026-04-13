using Microsoft.Extensions.Logging;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Mappers;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Repositories;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Leasing.Services;

public class TicketService : ITicketService
{
    private readonly ILeasingUnitOfWork _unitOfWork;
    private readonly ILeaseAccessService _leaseAccessService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ILeasingUnitOfWork unitOfWork,
        ILeaseAccessService leaseAccessService,
        INotificationService notificationService,
        ILogger<TicketService> logger)
    {
        _unitOfWork = unitOfWork;
        _leaseAccessService = leaseAccessService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<TicketDto> CreateTicketAsync(Guid leaseId, Guid tenantId, CreateTicketDto dto)
    {
        _logger.LogInformation("Creating ticket for lease {LeaseId} by tenant {TenantId}", leaseId, tenantId);

        // Verify tenant has access to this lease
        var lease = await _leaseAccessService.GetLeaseAccessContextAsync(leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        if (lease.TenantId != tenantId)
            throw new UnauthorizedAccessException("Apenas o inquilino pode criar tickets para este arrendamento.");

        // Validate DTO
        if (string.IsNullOrWhiteSpace(dto.Title) || dto.Title.Length < 5)
            throw new ArgumentException("Título deve ter pelo menos 5 caracteres.");

        if (string.IsNullOrWhiteSpace(dto.Description) || dto.Description.Length < 10)
            throw new ArgumentException("Descrição deve ter pelo menos 10 caracteres.");

        // Parse priority
        var priority = Enum.TryParse<TicketPriority>(dto.Priority, true, out var parsedPriority)
            ? parsedPriority
            : TicketPriority.Medium;

        // Create ticket
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = leaseId,
            TenantId = tenantId,
            LandlordId = lease.LandlordId,
            Title = dto.Title,
            Description = dto.Description,
            Priority = priority,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Tickets.AddAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Ticket {TicketId} created successfully", ticket.Id);

        // Send notification to landlord
        try
        {
            await _notificationService.SendNotificationAsync(
                lease.LandlordId,
                "Ticket",
                $"Novo ticket: {dto.Title}",
                ticket.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to landlord {LandlordId}", lease.LandlordId);
        }

        return ticket.ToDto();
    }

    public async Task<IEnumerable<TicketListItemDto>> GetTicketsByLeaseAsync(Guid leaseId, Guid userId)
    {
        _logger.LogInformation("Getting tickets for lease {LeaseId} by user {UserId}", leaseId, userId);

        // Verify user has access to this lease
        var lease = await _leaseAccessService.GetLeaseAccessContextAsync(leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        if (userId != lease.TenantId && userId != lease.LandlordId)
            throw new UnauthorizedAccessException("Acesso negado a este arrendamento.");

        var tickets = await _unitOfWork.Tickets.GetByLeaseIdAsync(leaseId);

        return tickets.Select(t => t.ToListDto());
    }

    public async Task<TicketDto?> GetTicketByIdAsync(Guid ticketId, Guid userId)
    {
        _logger.LogInformation("Getting ticket {TicketId} by user {UserId}", ticketId, userId);

        var ticket = await _unitOfWork.Tickets.GetByIdWithCommentsAndAttachmentsAsync(ticketId);

        if (ticket == null)
            return null;

        // Verify user has access
        if (userId != ticket.TenantId && userId != ticket.LandlordId)
            throw new UnauthorizedAccessException("Acesso negado a este ticket.");

        return ticket.ToDto();
    }

    public async Task<TicketDto> UpdateTicketStatusAsync(Guid ticketId, Guid userId, UpdateTicketStatusDto dto)
    {
        _logger.LogInformation("Updating ticket {TicketId} status by user {UserId}", ticketId, userId);

        var ticket = await _unitOfWork.Tickets.GetByIdWithCommentsAndAttachmentsAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket não encontrado.");

        // Only landlord can update status
        if (userId != ticket.LandlordId)
            throw new UnauthorizedAccessException("Apenas o senhorio pode atualizar o status do ticket.");

        // Parse new status
        var newStatus = Enum.TryParse<TicketStatus>(dto.Status, true, out var parsedStatus)
            ? parsedStatus
            : throw new ArgumentException("Status inválido.");

        // Validate state transition
        if (!IsValidStatusTransition(ticket.Status, newStatus))
            throw new InvalidOperationException($"Transição inválida de {ticket.Status} para {newStatus}.");

        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (newStatus == TicketStatus.Resolved)
            ticket.ResolvedAt = DateTime.UtcNow;

        await _unitOfWork.Tickets.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Ticket {TicketId} status updated to {Status}", ticketId, newStatus);

        // Send notification to tenant
        try
        {
            await _notificationService.SendNotificationAsync(
                ticket.TenantId,
                "TicketUpdate",
                $"Ticket \"{ticket.Title}\" atualizado: {newStatus}",
                ticket.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to tenant {TenantId}", ticket.TenantId);
        }

        return ticket.ToDto();
    }

    public async Task<TicketDto> AddCommentAsync(Guid ticketId, Guid userId, AddTicketCommentDto dto)
    {
        _logger.LogInformation("Adding comment to ticket {TicketId} by user {UserId}", ticketId, userId);

        var ticket = await _unitOfWork.Tickets.GetByIdWithCommentsAndAttachmentsAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket não encontrado.");

        // Verify user has access
        if (userId != ticket.TenantId && userId != ticket.LandlordId)
            throw new UnauthorizedAccessException("Acesso negado a este ticket.");

        if (string.IsNullOrWhiteSpace(dto.Content) || dto.Content.Length < 2)
            throw new ArgumentException("Comentário deve ter pelo menos 2 caracteres.");

        var comment = new TicketComment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            AuthorId = userId,
            Content = dto.Content,
            CreatedAt = DateTime.UtcNow
        };

        await _unitOfWork.Tickets.AddCommentAsync(comment);
        ticket.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Tickets.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Comment added to ticket {TicketId}", ticketId);

        // Send notification to the other party
        var recipientId = userId == ticket.TenantId ? ticket.LandlordId : ticket.TenantId;
        try
        {
            await _notificationService.SendNotificationAsync(
                recipientId,
                "TicketComment",
                $"Novo comentário no ticket \"{ticket.Title}\"",
                ticket.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", recipientId);
        }

        return ticket.ToDto();
    }

    public async Task<TicketDto> AddAttachmentAsync(Guid ticketId, Guid userId, string storageUrl, string fileName)
    {
        _logger.LogInformation("Adding attachment to ticket {TicketId} by user {UserId}", ticketId, userId);

        var ticket = await _unitOfWork.Tickets.GetByIdWithCommentsAndAttachmentsAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket não encontrado.");

        if (userId != ticket.TenantId && userId != ticket.LandlordId)
            throw new UnauthorizedAccessException("Acesso negado a este ticket.");

        var attachment = new TicketAttachment
        {
            Id = Guid.NewGuid(),
            TicketId = ticketId,
            StorageUrl = storageUrl,
            FileName = fileName,
            UploadedAt = DateTime.UtcNow
        };

        await _unitOfWork.Tickets.AddAttachmentAsync(attachment);
        ticket.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.Tickets.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Attachment added to ticket {TicketId}", ticketId);

        return ticket.ToDto();
    }

    private static bool IsValidStatusTransition(TicketStatus current, TicketStatus next)
    {
        return (current, next) switch
        {
            (TicketStatus.Open, TicketStatus.InProgress) => true,
            (TicketStatus.Open, TicketStatus.Resolved) => true,
            (TicketStatus.InProgress, TicketStatus.Resolved) => true,
            (TicketStatus.InProgress, TicketStatus.Open) => true,
            (TicketStatus.Resolved, TicketStatus.Closed) => true,
            (TicketStatus.Resolved, TicketStatus.Open) => true,
            _ => false
        };
    }
}
