using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Contracts.Database;

namespace TrustRent.Modules.Leasing.Repositories;

public class TicketRepository : ITicketRepository
{
    private readonly LeasingDbContext _context;

    public TicketRepository(LeasingDbContext context)
    {
        _context = context;
    }

    public async Task<Ticket?> GetByIdAsync(Guid id)
    {
        return await _context.Tickets.FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<Ticket?> GetByIdWithCommentsAndAttachmentsAsync(Guid id)
    {
        return await _context.Tickets
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .FirstOrDefaultAsync(t => t.Id == id);
    }

    public async Task<IEnumerable<Ticket>> GetByLeaseIdAsync(Guid leaseId)
    {
        return await _context.Tickets
            .Where(t => t.LeaseId == leaseId)
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task<IEnumerable<Ticket>> GetByPropertyIdAsync(Guid propertyId)
    {
        var leaseIds = await _context.Leases
            .Where(l => l.PropertyId == propertyId)
            .Select(l => l.Id)
            .ToListAsync();

        return await _context.Tickets
            .Where(t => leaseIds.Contains(t.LeaseId))
            .Include(t => t.Comments)
            .Include(t => t.Attachments)
            .OrderByDescending(t => t.CreatedAt)
            .ToListAsync();
    }

    public async Task AddAsync(Ticket ticket)
    {
        await _context.Tickets.AddAsync(ticket);
    }

    public async Task UpdateAsync(Ticket ticket)
    {
        _context.Tickets.Update(ticket);
        await Task.CompletedTask;
    }

    public async Task AddCommentAsync(TicketComment comment)
    {
        await _context.TicketComments.AddAsync(comment);
    }

    public async Task AddAttachmentAsync(TicketAttachment attachment)
    {
        await _context.TicketAttachments.AddAsync(attachment);
    }
}
