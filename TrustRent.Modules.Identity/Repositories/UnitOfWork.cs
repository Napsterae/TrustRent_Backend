using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;

namespace TrustRent.Modules.Identity.Repositories;

public class UnitOfWork : IUnitOfWork
{
    private readonly IdentityDbContext _context;
    private IUserRepository? _userRepository;

    public UnitOfWork(IdentityDbContext context) => _context = context;

    public IUserRepository Users => _userRepository ??= new UserRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        return await _context.SaveChangesAsync();
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}

