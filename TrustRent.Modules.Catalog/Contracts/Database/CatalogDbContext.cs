
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Database;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Property> Properties { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // MAGIA AQUI: Colocar as tabelas de imóveis no schema "catalog"
        modelBuilder.HasDefaultSchema("catalog");

        base.OnModelCreating(modelBuilder);
    }
}

