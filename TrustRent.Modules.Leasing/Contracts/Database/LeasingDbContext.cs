using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Leasing.Contracts.Database;

public class LeasingDbContext : DbContext
{
    public LeasingDbContext(DbContextOptions<LeasingDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }
    public DbSet<Payment> Payments { get; set; }
    public DbSet<StripeAccount> StripeAccounts { get; set; }
    public DbSet<TenantPaymentMethod> TenantPaymentMethods { get; set; }
    public DbSet<Lease> Leases { get; set; }
    public DbSet<LeaseHistory> LeaseHistories { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("leasing");

        // Ticket entity configuration
        modelBuilder.Entity<Ticket>(builder =>
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Title)
                   .IsRequired()
                   .HasMaxLength(200);

            builder.Property(t => t.Description)
                   .IsRequired()
                   .HasMaxLength(2000);

            builder.Property(t => t.Priority)
                   .HasConversion<string>()
                   .HasMaxLength(50);

            builder.Property(t => t.Status)
                   .HasConversion<string>()
                   .HasMaxLength(50);

            // Foreign keys
            builder.Property(t => t.LeaseId).IsRequired();
            builder.Property(t => t.TenantId).IsRequired();
            builder.Property(t => t.LandlordId).IsRequired();

            // Navigation properties
            builder.HasMany(t => t.Comments)
                   .WithOne(c => c.Ticket)
                   .HasForeignKey(c => c.TicketId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(t => t.Attachments)
                   .WithOne(a => a.Ticket)
                   .HasForeignKey(a => a.TicketId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Indexes
            builder.HasIndex(t => t.LeaseId);
            builder.HasIndex(t => t.Status);
            builder.HasIndex(t => t.Priority);
        });

        // TicketComment entity configuration
        modelBuilder.Entity<TicketComment>(builder =>
        {
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Content)
                   .IsRequired()
                   .HasMaxLength(2000);

            builder.Property(c => c.TicketId).IsRequired();
            builder.Property(c => c.AuthorId).IsRequired();

            builder.HasIndex(c => c.TicketId);
        });

        // TicketAttachment entity configuration
        modelBuilder.Entity<TicketAttachment>(builder =>
        {
            builder.HasKey(a => a.Id);

            builder.Property(a => a.StorageUrl)
                   .IsRequired()
                   .HasMaxLength(500);

            builder.Property(a => a.FileName)
                   .IsRequired()
                   .HasMaxLength(255);

            builder.Property(a => a.TicketId).IsRequired();

            builder.HasIndex(a => a.TicketId);
        });

        // Payment entity configuration
        modelBuilder.Entity<Payment>(builder =>
        {
            builder.HasKey(p => p.Id);
            builder.Property(p => p.StripePaymentIntentId).IsRequired().HasMaxLength(200);
            builder.Property(p => p.StripeTransferId).HasMaxLength(200);
            builder.Property(p => p.Type).HasConversion<string>().HasMaxLength(50);
            builder.Property(p => p.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(p => p.Currency).HasMaxLength(10);
            builder.Property(p => p.Amount).HasPrecision(18, 2);
            builder.Property(p => p.PlatformFee).HasPrecision(18, 2);
            builder.Property(p => p.LandlordAmount).HasPrecision(18, 2);
            builder.Property(p => p.RentAmount).HasPrecision(18, 2);
            builder.Property(p => p.DepositAmount).HasPrecision(18, 2);
            builder.Property(p => p.AdvanceRentAmount).HasPrecision(18, 2);
            builder.Property(p => p.FailureReason).HasMaxLength(500);
            builder.HasIndex(p => p.LeaseId);
            builder.HasIndex(p => p.StripePaymentIntentId).IsUnique();
            builder.HasIndex(p => p.TenantId);
        });

        // StripeAccount entity configuration
        modelBuilder.Entity<StripeAccount>(builder =>
        {
            builder.HasKey(s => s.Id);
            builder.Property(s => s.StripeAccountId).IsRequired().HasMaxLength(200);
            builder.HasIndex(s => s.StripeAccountId).IsUnique();
            builder.HasIndex(s => new { s.UserId, s.PropertyId }).IsUnique();
            builder.HasIndex(s => s.UserId);
        });

        // TenantPaymentMethod entity configuration
        modelBuilder.Entity<TenantPaymentMethod>(builder =>
        {
            builder.HasKey(t => t.Id);
            builder.Property(t => t.StripePaymentMethodId).IsRequired().HasMaxLength(200);
            builder.Property(t => t.CardBrand).HasMaxLength(50);
            builder.Property(t => t.CardLast4).HasMaxLength(4);
            builder.HasIndex(t => t.StripePaymentMethodId).IsUnique();
            builder.HasIndex(t => t.UserId);
        });

        // Lease entity configuration
        modelBuilder.Entity<Lease>(builder =>
        {
            builder.HasKey(l => l.Id);

            builder.HasMany(l => l.History)
                   .WithOne(h => h.Lease)
                   .HasForeignKey(h => h.LeaseId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.Property(l => l.Status).HasConversion<string>().HasMaxLength(50);
            builder.Property(l => l.ContractType).HasMaxLength(50);
            builder.Property(l => l.LeaseRegime).HasMaxLength(100);
            builder.Property(l => l.AdvanceRentMonths).HasDefaultValue(0);
            builder.Property(l => l.ContractFilePath).HasMaxLength(500);
            builder.Property(l => l.LandlordSignatureRef).HasMaxLength(200);
            builder.Property(l => l.TenantSignatureRef).HasMaxLength(200);

            builder.HasIndex(l => l.ApplicationId);
            builder.HasIndex(l => l.PropertyId);
        });

        // LeaseHistory entity configuration
        modelBuilder.Entity<LeaseHistory>(builder =>
        {
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Action).IsRequired().HasMaxLength(100);
        });
    }
}
