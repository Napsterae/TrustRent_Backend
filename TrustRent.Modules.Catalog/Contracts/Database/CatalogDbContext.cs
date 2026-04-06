
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
    public DbSet<Amenity> Amenities { get; set; }
    public DbSet<PropertyAmenity> PropertyAmenities { get; set; }

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

        modelBuilder.Entity<Amenity>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.Property(a => a.Name).IsRequired().HasMaxLength(100);
            builder.Property(a => a.IconName).IsRequired().HasMaxLength(50);
            builder.Property(a => a.Category).IsRequired().HasMaxLength(50);
        });

        modelBuilder.Entity<PropertyAmenity>(builder =>
        {
            builder.HasKey(pa => new { pa.PropertyId, pa.AmenityId });

            builder.HasOne(pa => pa.Property)
                   .WithMany(p => p.Amenities)
                   .HasForeignKey(pa => pa.PropertyId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(pa => pa.Amenity)
                   .WithMany()
                   .HasForeignKey(pa => pa.AmenityId)
                   .OnDelete(DeleteBehavior.Cascade);
        });

        // Seed de Comodidades
        modelBuilder.Entity<Amenity>().HasData(
            // Básico
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000001"), Name = "Wifi", IconName = "Wifi", Category = "Básico" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000002"), Name = "Cozinha Equipada", IconName = "Utensils", Category = "Básico" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000003"), Name = "Máquina de Lavar", IconName = "WashingMachine", Category = "Básico" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000004"), Name = "Ferro de Engomar", IconName = "Iron", Category = "Básico" },
            // Conforto
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000005"), Name = "Ar Condicionado", IconName = "Wind", Category = "Conforto" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000006"), Name = "Aquecimento Central", IconName = "Thermometer", Category = "Conforto" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000007"), Name = "Televisão", IconName = "Tv", Category = "Conforto" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000008"), Name = "Berço", IconName = "Baby", Category = "Conforto" },
            // Lazer
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000009"), Name = "Piscina", IconName = "Waves", Category = "Lazer" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000a"), Name = "Jacuzzi", IconName = "Bath", Category = "Lazer" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000b"), Name = "Ginásio", IconName = "Dumbbell", Category = "Lazer" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000c"), Name = "Churrasqueira", IconName = "Flame", Category = "Lazer" },
            // Segurança
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000d"), Name = "Extintor", IconName = "FireExtinguisher", Category = "Segurança" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000e"), Name = "Detetor de Fumo", IconName = "Siren", Category = "Segurança" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-00000000000f"), Name = "Alarme", IconName = "ShieldAlert", Category = "Segurança" },
            // Extra
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000010"), Name = "Aceita Animais", IconName = "Dog", Category = "Extra" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000011"), Name = "Próximo de Supermercado", IconName = "ShoppingCart", Category = "Extra" },
            new Amenity { Id = Guid.Parse("a0000000-0000-0000-0000-000000000012"), Name = "Próximo de Transporte", IconName = "Bus", Category = "Extra" }
        );

        base.OnModelCreating(modelBuilder);
    }
}

