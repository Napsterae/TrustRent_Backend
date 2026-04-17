using TrustRent.Modules.Leasing.Contracts.DTOs;

namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

public interface ITicketService
{
    Task<TicketDto> CreateTicketAsync(Guid leaseId, Guid tenantId, CreateTicketDto dto);
    Task<IEnumerable<TicketListItemDto>> GetTicketsByLeaseAsync(Guid leaseId, Guid userId);
    Task<IEnumerable<TicketListItemDto>> GetTicketsByPropertyAsync(Guid propertyId, Guid userId);
    Task<TicketDto?> GetTicketByIdAsync(Guid ticketId, Guid userId);
    Task<TicketDto> UpdateTicketStatusAsync(Guid ticketId, Guid userId, UpdateTicketStatusDto dto);
    Task<TicketDto> AddCommentAsync(Guid ticketId, Guid userId, AddTicketCommentDto dto);
    Task<TicketDto> AddAttachmentAsync(Guid ticketId, Guid userId, string storageUrl, string fileName);
}
