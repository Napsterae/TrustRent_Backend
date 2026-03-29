using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Repositories;

public class CatalogUnitOfWork : ICatalogUnitOfWork
{
    private readonly CatalogDbContext _context;
    private IPropertyRepository? _propertyRepository;

    public CatalogUnitOfWork(CatalogDbContext context) => _context = context;

    public IPropertyRepository Properties => _propertyRepository ??= new PropertyRepository(_context);

    public async Task<int> SaveChangesAsync() => await _context.SaveChangesAsync();

    public void Dispose() => _context.Dispose();
}