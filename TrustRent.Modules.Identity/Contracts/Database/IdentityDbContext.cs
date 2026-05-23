using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Security;

namespace TrustRent.Modules.Identity.Contracts.Database;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }
    public DbSet<EmailLoginCode> EmailLoginCodes { get; set; }
    public DbSet<WhatsAppOneTimeCode> WhatsAppOneTimeCodes { get; set; }

    public DbSet<PhoneCountry> PhoneCountries { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Nif).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.CitizenCardNumber).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.PhoneNumber).IsUnique();

        modelBuilder.Entity<User>()
        .Property(u => u.CitizenCardNumber)
        .HasConversion(
            v => v == null ? null : EncryptionHelper.Encrypt(v),
            v => v == null ? null : EncryptionHelper.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.PhoneNumber)
        .HasConversion(
            v => v == null ? null : EncryptionHelper.Encrypt(v),
            v => v == null ? null : EncryptionHelper.Decrypt(v)
        );

        modelBuilder.Entity<EmailLoginCode>(b =>
        {
            b.ToTable("EmailLoginCodes", "identity");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).IsRequired().HasMaxLength(320);
            b.Property(x => x.CodeHash).IsRequired().HasMaxLength(128);
            b.Property(x => x.RequestedUserAgent).HasMaxLength(1024);
            b.Property(x => x.RequestedFromIp).HasMaxLength(128);
            b.HasIndex(x => new { x.Email, x.RequestedAt });
            b.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<WhatsAppOneTimeCode>(b =>
        {
            b.ToTable("WhatsAppOneTimeCodes", "identity");
            b.HasKey(x => x.Id);
            b.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(32);
            b.Property(x => x.Purpose).IsRequired().HasMaxLength(64);
            b.Property(x => x.CodeHash).IsRequired().HasMaxLength(128);
            b.Property(x => x.RequestedUserAgent).HasMaxLength(1024);
            b.Property(x => x.RequestedFromIp).HasMaxLength(128);
            b.HasIndex(x => new { x.PhoneNumber, x.Purpose, x.RequestedAt });
            b.HasIndex(x => x.ExpiresAt);
            b.HasIndex(x => new { x.UserId, x.Purpose, x.RequestedAt });
        });

        modelBuilder.Entity<PhoneCountry>(b =>
        {
            b.ToTable("PhoneCountries", "identity");
            b.HasKey(x => x.Id);
            b.Property(x => x.IsoCode).IsRequired().HasMaxLength(3);
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.DialCode).IsRequired().HasMaxLength(10);
            b.Property(x => x.MobilePattern).IsRequired().HasMaxLength(200);
            b.Property(x => x.Example).HasMaxLength(40);
            b.Property(x => x.FlagEmoji).HasMaxLength(10);
            b.HasIndex(x => x.IsoCode).IsUnique();
        });

        base.OnModelCreating(modelBuilder);
    }
}

