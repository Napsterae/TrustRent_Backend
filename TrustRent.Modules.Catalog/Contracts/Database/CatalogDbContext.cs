
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Modules.Catalog.Contracts.Database;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Property> Properties { get; set; }
    public DbSet<PropertyImage> PropertyImages { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationHistory> ApplicationHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("catalog");

        modelBuilder.Entity<Property>(builder =>
        {
            builder.HasKey(p => p.Id);

            builder.HasMany(p => p.Images)
                   .WithOne()
                   .HasForeignKey(i => i.PropertyId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(p => p.MatrixArticle).HasMaxLength(100);
            builder.Property(p => p.PropertyFraction).HasMaxLength(10);
            builder.Property(p => p.EnergyClass).HasMaxLength(5);
            builder.Property(p => p.EnergyCertificateNumber).HasMaxLength(100);
            builder.Property(p => p.AtRegistrationNumber).HasMaxLength(100);
        });

        modelBuilder.Entity<PropertyImage>(builder =>
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Url).IsRequired();
        });

        modelBuilder.Entity<Application>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.HasOne<Property>().WithMany().HasForeignKey(a => a.PropertyId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(a => a.History).WithOne().HasForeignKey(h => h.ApplicationId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ApplicationHistory>(builder =>
        {
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Action).IsRequired().HasMaxLength(100);
        });

        base.OnModelCreating(modelBuilder);
    }
}

