using Microsoft.Extensions.Logging;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Mappers;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Repositories;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Leasing.Services;

public class TicketService : ITicketService
{
    private readonly ILeasingUnitOfWork _unitOfWork;
    private readonly ILeaseAccessService _leaseAccessService;
    private readonly ICatalogAccessService _catalogAccessService;
    private readonly IUserService _userService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<TicketService> _logger;

    public TicketService(
        ILeasingUnitOfWork unitOfWork,
        ILeaseAccessService leaseAccessService,
        ICatalogAccessService catalogAccessService,
        IUserService userService,
        INotificationService notificationService,
        ILogger<TicketService> logger)
    {
        _unitOfWork = unitOfWork;
        _leaseAccessService = leaseAccessService;
        _catalogAccessService = catalogAccessService;
        _userService = userService;
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

        var items = tickets.Select(t => t.ToListDto()).ToList();
        await EnrichListItemsWithTenantInfoAsync(items);
        return items;
    }

    public async Task<IEnumerable<TicketListItemDto>> GetTicketsByPropertyAsync(Guid propertyId, Guid userId)
    {
        _logger.LogInformation("Getting tickets for property {PropertyId} by user {UserId}", propertyId, userId);

        // Verify user is the landlord of this property
        var property = await _catalogAccessService.GetPropertyContextAsync(propertyId)
            ?? throw new KeyNotFoundException("Imóvel não encontrado.");

        if (property.LandlordId != userId)
            throw new UnauthorizedAccessException("Apenas o proprietário pode ver tickets deste imóvel.");

        var tickets = await _unitOfWork.Tickets.GetByPropertyIdAsync(propertyId);

        var items = tickets.Select(t => t.ToListDto()).ToList();
        await EnrichListItemsWithTenantInfoAsync(items);
        return items;
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

        var dto = ticket.ToDto();
        var tenantProfile = await _userService.GetProfileDtoAsync(ticket.TenantId);
        if (tenantProfile != null)
        {
            dto.TenantName = tenantProfile.Name;
            dto.TenantProfilePictureUrl = tenantProfile.ProfilePictureUrl;
        }
        return dto;
    }

    public async Task<TicketDto> UpdateTicketStatusAsync(Guid ticketId, Guid userId, UpdateTicketStatusDto dto)
    {
        _logger.LogInformation("Updating ticket {TicketId} status by user {UserId}", ticketId, userId);

        var ticket = await _unitOfWork.Tickets.GetByIdWithCommentsAndAttachmentsAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket não encontrado.");

        // Validate role-based status update permissions
        var isLandlord = userId == ticket.LandlordId;
        var isTenant = userId == ticket.TenantId;
        if (!isLandlord && !isTenant)
            throw new UnauthorizedAccessException("Apenas o senhorio ou inquilino podem atualizar o status do ticket.");

        // Parse new status
        var newStatus = Enum.TryParse<TicketStatus>(dto.Status, true, out var parsedStatus)
            ? parsedStatus
            : throw new ArgumentException("Status inválido.");

        // Validate state transition
        if (!IsValidStatusTransition(ticket.Status, newStatus))
            throw new InvalidOperationException($"Transição inválida de {ticket.Status} para {newStatus}.");

        // Role-based transition rules: tenant can only mark as Resolved, landlord handles the rest
        if (isTenant && newStatus != TicketStatus.Resolved)
            throw new UnauthorizedAccessException("O inquilino apenas pode marcar o ticket como resolvido.");
        if (isLandlord && newStatus == TicketStatus.Resolved)
            throw new UnauthorizedAccessException("Apenas o inquilino pode marcar o ticket como resolvido.");

        ticket.Status = newStatus;
        ticket.UpdatedAt = DateTime.UtcNow;

        if (newStatus == TicketStatus.Resolved)
            ticket.ResolvedAt = DateTime.UtcNow;

        await _unitOfWork.Tickets.UpdateAsync(ticket);
        await _unitOfWork.SaveChangesAsync();

        _logger.LogInformation("Ticket {TicketId} status updated to {Status}", ticketId, newStatus);

        // Send notification to the other party
        var notifyUserId = isTenant ? ticket.LandlordId : ticket.TenantId;
        try
        {
            await _notificationService.SendNotificationAsync(
                notifyUserId,
                "TicketUpdate",
                $"Ticket \"{ticket.Title}\" atualizado: {newStatus}",
                ticket.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send notification to user {UserId}", notifyUserId);
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

    private async Task EnrichListItemsWithTenantInfoAsync(List<TicketListItemDto> items)
    {
        var tenantIds = items.Select(i => i.TenantId).Distinct().ToList();
        var profiles = new Dictionary<Guid, (string Name, string? Avatar)>();

        foreach (var tenantId in tenantIds)
        {
            var profile = await _userService.GetProfileDtoAsync(tenantId);
            if (profile != null)
                profiles[tenantId] = (profile.Name, profile.ProfilePictureUrl);
        }

        foreach (var item in items)
        {
            if (profiles.TryGetValue(item.TenantId, out var info))
            {
                item.TenantName = info.Name;
                item.TenantProfilePictureUrl = info.Avatar;
            }
        }
    }
}
