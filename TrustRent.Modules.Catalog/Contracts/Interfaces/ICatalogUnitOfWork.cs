namespace TrustRent.Modules.Catalog.Contracts.Interfaces;

public interface ICatalogUnitOfWork : IDisposable
{
    IPropertyRepository Properties { get; }
    Task<int> SaveChangesAsync();
}