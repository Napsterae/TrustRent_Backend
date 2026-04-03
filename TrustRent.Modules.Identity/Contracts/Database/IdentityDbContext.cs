using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Identity.Models;
using TrustRent.Shared.Security;

namespace TrustRent.Modules.Identity.Contracts.Database;

public class IdentityDbContext : DbContext
{
    public IdentityDbContext(DbContextOptions<IdentityDbContext> options) : base(options) { }

    public DbSet<User> Users { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("identity");

        modelBuilder.Entity<User>().HasIndex(u => u.Email).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.Nif).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.CitizenCardNumber).IsUnique();

        modelBuilder.Entity<User>()
        .Property(u => u.CitizenCardNumber)
        .HasConversion(
            v => v == null ? null : EncryptionHelper.Encrypt(v),
            v => v == null ? null : EncryptionHelper.Decrypt(v)
        );

        base.OnModelCreating(modelBuilder);
    }
}

