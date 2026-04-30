
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Modules.Catalog.Models.ReferenceData;

namespace TrustRent.Modules.Catalog.Contracts.Database;

public class CatalogDbContext : DbContext
{
    public CatalogDbContext(DbContextOptions<CatalogDbContext> options) : base(options) { }

    public DbSet<Property> Properties { get; set; }
    public DbSet<PropertyImage> PropertyImages { get; set; }
    public DbSet<Application> Applications { get; set; }
    public DbSet<ApplicationHistory> ApplicationHistories { get; set; }
    public DbSet<ApplicationCoTenantInvite> ApplicationCoTenantInvites { get; set; }
    public DbSet<Guarantor> Guarantors { get; set; }
    public DbSet<Amenity> Amenities { get; set; }
    public DbSet<PropertyAmenity> PropertyAmenities { get; set; }
    public DbSet<PropertyPeriodicity> PropertyPeriodicities { get; set; }

    // Reference data (editável via back-office futuro)
    public DbSet<District> Districts { get; set; }
    public DbSet<Municipality> Municipalities { get; set; }
    public DbSet<Parish> Parishes { get; set; }
    public DbSet<PropertyType> PropertyTypes { get; set; }
    public DbSet<Typology> Typologies { get; set; }
    public DbSet<SalaryRange> SalaryRanges { get; set; }

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
            builder.Property(p => p.AdvanceRentMonths).HasDefaultValue(0);
            builder.Property(p => p.GuarantorPolicyNote).HasMaxLength(500);
        });

        modelBuilder.Entity<PropertyImage>(builder =>
        {
            builder.HasKey(i => i.Id);
            builder.Property(i => i.Url).IsRequired();
        });

        modelBuilder.Entity<Application>(builder =>
        {
            builder.HasKey(a => a.Id);
            builder.HasOne(a => a.Property).WithMany().HasForeignKey(a => a.PropertyId).OnDelete(DeleteBehavior.Cascade);
            builder.HasMany(a => a.History).WithOne().HasForeignKey(h => h.ApplicationId).OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(a => a.IncomeRange)
                   .WithMany()
                   .HasForeignKey(a => a.IncomeRangeId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasOne(a => a.CoTenantIncomeRange)
                   .WithMany()
                   .HasForeignKey(a => a.CoTenantIncomeRangeId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.Property(a => a.EmploymentType).HasConversion<int>();
            builder.Property(a => a.IncomeValidationMethod).HasConversion<int>();
            builder.Property(a => a.EmployerName).HasMaxLength(200);
            builder.Property(a => a.EmployerNif).HasMaxLength(9);

            builder.Property(a => a.CoTenantEmploymentType).HasConversion<int>();
            builder.Property(a => a.CoTenantIncomeValidationMethod).HasConversion<int>();
            builder.Property(a => a.CoTenantEmployerName).HasMaxLength(200);
            builder.Property(a => a.CoTenantEmployerNif).HasMaxLength(9);

            builder.Property(a => a.GuarantorRequirementStatus).HasConversion<int>();
            builder.Property(a => a.GuarantorRequestNote).HasMaxLength(500);

            builder.HasMany(a => a.CoTenantInvites)
                   .WithOne(i => i.Application)
                   .HasForeignKey(i => i.ApplicationId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(a => a.Guarantors)
                   .WithOne(g => g.Application)
                   .HasForeignKey(g => g.ApplicationId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(a => a.CoTenantUserId);
            builder.HasIndex(a => a.GuarantorId);
        });

        // ===== ApplicationCoTenantInvite =====
        modelBuilder.Entity<ApplicationCoTenantInvite>(builder =>
        {
            builder.ToTable("ApplicationCoTenantInvites", "catalog");
            builder.HasKey(i => i.Id);
            builder.Property(i => i.InviteeEmail).IsRequired().HasMaxLength(320);
            builder.Property(i => i.Status).HasConversion<int>();
            builder.Property(i => i.DeclineReason).HasMaxLength(500);
            builder.Property(i => i.CreatedFromIp).HasMaxLength(45);

            builder.HasIndex(i => new { i.InviteeUserId, i.Status, i.ExpiresAt });
            // Apenas 1 convite Pending por (Application, Email)
            builder.HasIndex(i => new { i.ApplicationId, i.InviteeEmail })
                   .IsUnique()
                   .HasFilter("\"Status\" = 0");
            // Apenas 1 convite Pending/Accepted por candidatura
            builder.HasIndex(i => i.ApplicationId)
                   .IsUnique()
                   .HasFilter("\"Status\" IN (0,1)");
        });

        // ===== Guarantor =====
        modelBuilder.Entity<Guarantor>(builder =>
        {
            builder.ToTable("Guarantors", "catalog");
            builder.HasKey(g => g.Id);
            builder.Property(g => g.InviteStatus).HasConversion<int>();
            builder.Property(g => g.UserId).IsRequired(false);
            builder.Property(g => g.GuestEmail).IsRequired().HasMaxLength(320);
            builder.Property(g => g.GuestName).HasMaxLength(200);
            builder.Property(g => g.GuestPhoneNumber).HasMaxLength(30);
            builder.Property(g => g.GuestPostalCode).HasMaxLength(20);
            builder.Property(g => g.GuestAccessToken).IsRequired().HasMaxLength(128);
            builder.Property(g => g.CreatedFromIp).HasMaxLength(45);
            builder.Property(g => g.EmploymentType).HasConversion<int>();
            builder.Property(g => g.IncomeValidationMethod).HasConversion<int>();
            builder.Property(g => g.LandlordRequestNote).HasMaxLength(500);
            builder.Property(g => g.RejectionReason).HasMaxLength(500);
            builder.Property(g => g.DeclineReason).HasMaxLength(500);
            builder.Property(g => g.IdentityMatchEvidenceHash).HasMaxLength(128);
            builder.Property(g => g.EmployerName).HasMaxLength(200);
            builder.Property(g => g.EmployerNif).HasMaxLength(9);

            builder.HasOne(g => g.IncomeRange)
                   .WithMany()
                   .HasForeignKey(g => g.IncomeRangeId)
                   .OnDelete(DeleteBehavior.SetNull);

            builder.HasIndex(g => new { g.UserId, g.InviteStatus });
            builder.HasIndex(g => g.GuestAccessToken).IsUnique();
            builder.HasIndex(g => new { g.GuestEmail, g.InviteStatus });
            // Apenas 1 fiador "ativo" por candidatura (Pending/Accepted)
            builder.HasIndex(g => g.ApplicationId)
                   .IsUnique()
                   .HasFilter("\"InviteStatus\" IN (0,1)");
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

        // PropertyPeriodicity
        modelBuilder.Entity<PropertyPeriodicity>(entity =>
        {
            entity.ToTable("PropertyPeriodicities", "catalog");
            entity.HasKey(pp => pp.Id);
            entity.HasOne<Property>()
                  .WithMany(p => p.AcceptedPeriodicities)
                  .HasForeignKey(pp => pp.PropertyId)
                  .OnDelete(DeleteBehavior.Cascade);
        });

        // LeaseRegime como string na BD
        modelBuilder.Entity<Property>()
            .Property(p => p.LeaseRegime)
            .HasConversion<string>();

        // ===== Reference Data =====
        modelBuilder.Entity<District>(b =>
        {
            b.ToTable("Districts", "catalog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(80);
            b.Property(x => x.Name).IsRequired().HasMaxLength(150);
            b.HasIndex(x => x.Code).IsUnique();
            b.HasMany(x => x.Municipalities)
             .WithOne(m => m.District)
             .HasForeignKey(m => m.DistrictId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Municipality>(b =>
        {
            b.ToTable("Municipalities", "catalog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(120);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.HasIndex(x => new { x.DistrictId, x.Code }).IsUnique();
            b.HasMany(x => x.Parishes)
             .WithOne(p => p.Municipality)
             .HasForeignKey(p => p.MunicipalityId)
             .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Parish>(b =>
        {
            b.ToTable("Parishes", "catalog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(200);
            b.Property(x => x.Name).IsRequired().HasMaxLength(300);
            b.HasIndex(x => new { x.MunicipalityId, x.Code }).IsUnique();
        });

        modelBuilder.Entity<PropertyType>(b =>
        {
            b.ToTable("PropertyTypes", "catalog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(50);
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<Typology>(b =>
        {
            b.ToTable("Typologies", "catalog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(30);
            b.Property(x => x.Name).IsRequired().HasMaxLength(50);
            b.HasIndex(x => x.Code).IsUnique();
        });

        modelBuilder.Entity<SalaryRange>(b =>
        {
            b.ToTable("SalaryRanges", "catalog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Code).IsRequired().HasMaxLength(32);
            b.Property(x => x.Label).IsRequired().HasMaxLength(64);
            b.Property(x => x.MinAmount).HasColumnType("numeric(12,2)");
            b.Property(x => x.MaxAmount).HasColumnType("numeric(12,2)");
            b.HasIndex(x => x.Code).IsUnique();
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

