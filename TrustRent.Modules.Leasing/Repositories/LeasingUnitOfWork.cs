using TrustRent.Modules.Leasing.Contracts.Database;

namespace TrustRent.Modules.Leasing.Repositories;

public class LeasingUnitOfWork : ILeasingUnitOfWork
{
    private readonly LeasingDbContext _context;
    private ITicketRepository? _ticketRepository;

    public LeasingUnitOfWork(LeasingDbContext context)
    {
        _context = context;
    }

    public ITicketRepository Tickets => _ticketRepository ??= new TicketRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}
