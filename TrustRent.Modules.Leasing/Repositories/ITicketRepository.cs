using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Leasing.Repositories;

public interface ITicketRepository
{
    Task<Ticket?> GetByIdAsync(Guid id);
    Task<Ticket?> GetByIdWithCommentsAndAttachmentsAsync(Guid id);
    Task<IEnumerable<Ticket>> GetByLeaseIdAsync(Guid leaseId);
    Task<IEnumerable<Ticket>> GetByPropertyIdAsync(Guid propertyId);
    Task AddAsync(Ticket ticket);
    Task UpdateAsync(Ticket ticket);
    Task AddCommentAsync(TicketComment comment);
    Task AddAttachmentAsync(TicketAttachment attachment);
}
