namespace TrustRent.Modules.Leasing.Repositories;

public interface ILeasingUnitOfWork : IDisposable
{
    ITicketRepository Tickets { get; }
    Task<int> SaveChangesAsync();
}
