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

        modelBuilder.Entity<User>().HasIndex(u => u.EmailBlindIndex).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.NifBlindIndex).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.CitizenCardNumberBlindIndex).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.PhoneNumberBlindIndex).IsUnique();

        modelBuilder.Entity<User>().Property(u => u.EmailBlindIndex).HasMaxLength(128);
        modelBuilder.Entity<User>().Property(u => u.NifBlindIndex).HasMaxLength(128);
        modelBuilder.Entity<User>().Property(u => u.CitizenCardNumberBlindIndex).HasMaxLength(128);
        modelBuilder.Entity<User>().Property(u => u.PhoneNumberBlindIndex).HasMaxLength(128);

        modelBuilder.Entity<User>()
        .Property(u => u.Email)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.Nif)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.Name)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.Address)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.PostalCode)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.CitizenCardNumber)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.PhoneNumber)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.PendingPhoneNumber)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<User>()
        .Property(u => u.TelegramPendingExpectedPhoneNumber)
        .HasConversion(
            v => v == null ? null : EncryptionHelperV2.Encrypt(v),
            v => v == null ? null : EncryptionHelperV2.Decrypt(v)
        );

        modelBuilder.Entity<EmailLoginCode>(b =>
        {
            b.ToTable("EmailLoginCodes", "identity");
            b.HasKey(x => x.Id);
            b.Property(x => x.Email).IsRequired().HasMaxLength(2000)
                .HasConversion(
                    v => v == null ? null : EncryptionHelperV2.Encrypt(v),
                    v => v == null ? null : EncryptionHelperV2.Decrypt(v)
                );
            b.Property(x => x.CodeHash).IsRequired().HasMaxLength(128);
            b.Property(x => x.RequestedUserAgent).HasMaxLength(1024);
            b.Property(x => x.RequestedFromIp).HasMaxLength(128);
            b.Property(x => x.EmailBlindIndex).HasMaxLength(128);
            b.HasIndex(x => new { x.EmailBlindIndex, x.RequestedAt });
            b.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<WhatsAppOneTimeCode>(b =>
        {
            b.ToTable("WhatsAppOneTimeCodes", "identity");
            b.HasKey(x => x.Id);
            b.Property(x => x.PhoneNumber).IsRequired().HasMaxLength(2000)
                .HasConversion(
                    v => v == null ? null : EncryptionHelperV2.Encrypt(v),
                    v => v == null ? null : EncryptionHelperV2.Decrypt(v)
                );
            b.Property(x => x.Purpose).IsRequired().HasMaxLength(64);
            b.Property(x => x.CodeHash).IsRequired().HasMaxLength(128);
            b.Property(x => x.RequestedUserAgent).HasMaxLength(1024);
            b.Property(x => x.RequestedFromIp).HasMaxLength(128);
            b.Property(x => x.PhoneNumberBlindIndex).HasMaxLength(128);
            b.HasIndex(x => new { x.PhoneNumberBlindIndex, x.Purpose, x.UserId, x.RequestedAt });
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

