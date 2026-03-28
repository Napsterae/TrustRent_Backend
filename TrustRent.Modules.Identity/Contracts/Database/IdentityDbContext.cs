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


        // SEED: Criar um utilizador de teste quando a base de dados for gerada
        var adminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        modelBuilder.Entity<User>().HasData(new User
        {
            Id = adminId,
            Name = "João Silva",
            Email = "joao.silva@email.pt",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("TrustRent2026!"),
            TrustScore = 85
        });

        base.OnModelCreating(modelBuilder);
    }
}

