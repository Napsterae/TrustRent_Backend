
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Database;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Property> Properties { get; set; }
    public DbSet<PropertyImage> PropertyImages { get; set; }
    public DbSet<PropertyDocument> PropertyDocuments { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        // Configurar as relações (1 Imóvel tem N Imagens/Documentos)
        modelBuilder.Entity<Property>()
            .HasMany(p => p.Images)
            .WithOne()
            .HasForeignKey(img => img.PropertyId)
            .OnDelete(DeleteBehavior.Cascade); // Se apagar a casa, apaga as referências das fotos

        modelBuilder.Entity<Property>()
            .HasMany(p => p.Documents)
            .WithOne()
            .HasForeignKey(doc => doc.PropertyId)
            .OnDelete(DeleteBehavior.Cascade);

        base.OnModelCreating(modelBuilder);
    }
}

