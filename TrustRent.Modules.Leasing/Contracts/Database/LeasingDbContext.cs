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
    public DbSet<Review> Reviews { get; set; }
    public DbSet<LeaseRenewalNotification> LeaseRenewalNotifications { get; set; }
    public DbSet<LegalCommunicationLog> LegalCommunicationLogs { get; set; }
    public DbSet<LeaseTerminationRequest> LeaseTerminationRequests { get; set; }
    public DbSet<RentIncreaseRequest> RentIncreaseRequests { get; set; }
    public DbSet<LeaseSignature> LeaseSignatures { get; set; }
    public DbSet<LeaseTermAcceptance> LeaseTermAcceptances { get; set; }

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
            builder.Property(t => t.Type).IsRequired().HasMaxLength(50).HasDefaultValue("card");
            builder.Property(t => t.DisplayName).HasMaxLength(200);
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
            builder.HasIndex(l => l.CoTenantId);
            builder.HasIndex(l => l.GuarantorUserId);
        });

        // LeaseHistory entity configuration
        modelBuilder.Entity<LeaseHistory>(builder =>
        {
            builder.HasKey(h => h.Id);
            builder.Property(h => h.Action).IsRequired().HasMaxLength(100);
        });

        // Review entity configuration
        modelBuilder.Entity<Review>(builder =>
        {
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Rating).IsRequired();
            builder.Property(r => r.Comment).HasMaxLength(2000);
            builder.Property(r => r.Type).HasConversion<string>().HasMaxLength(50);
            builder.Property(r => r.Status).HasConversion<string>().HasMaxLength(50);

            builder.HasIndex(r => r.ReviewerId);
            builder.HasIndex(r => r.ReviewedUserId);
            builder.HasIndex(r => r.LeaseId);
            builder.HasIndex(r => r.TicketId);
            builder.HasIndex(r => r.PairId);
            builder.HasIndex(r => r.Status);
        });

        // LeaseRenewalNotification entity configuration
        modelBuilder.Entity<LeaseRenewalNotification>(builder =>
        {
            builder.HasKey(n => n.Id);

            builder.Property(n => n.LandlordResponse).HasMaxLength(20);
            builder.Property(n => n.TenantResponse).HasMaxLength(20);

            builder.HasIndex(n => n.LeaseId);
            builder.HasIndex(n => n.Processed);
        });

        // LegalCommunicationLog entity configuration
        modelBuilder.Entity<LegalCommunicationLog>(builder =>
        {
            builder.HasKey(l => l.Id);
            builder.Property(l => l.CommunicationType).IsRequired().HasMaxLength(100);
            builder.Property(l => l.Content).IsRequired();
            builder.Property(l => l.SenderIpAddress).IsRequired().HasMaxLength(45);
            builder.Property(l => l.SenderUserAgent).HasMaxLength(500);
            builder.Property(l => l.ViewerIpAddress).HasMaxLength(45);
            builder.Property(l => l.ViewerUserAgent).HasMaxLength(500);
            builder.Property(l => l.AcknowledgerIpAddress).HasMaxLength(45);
            builder.Property(l => l.EmailRecipientAddress).HasMaxLength(320);
            builder.Property(l => l.ContentHash).HasMaxLength(64);
            builder.HasIndex(l => l.LeaseId);
            builder.HasIndex(l => l.SenderId);
            builder.HasIndex(l => l.RecipientId);
            builder.HasIndex(l => l.CommunicationType);
            builder.HasIndex(l => l.SentAt);
        });

        // ===== LeaseSignature (assinaturas multi-parte) =====
        modelBuilder.Entity<LeaseSignature>(builder =>
        {
            builder.ToTable("LeaseSignatures", "leasing");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Role).HasConversion<int>();
            builder.Property(s => s.SignatureRef).HasMaxLength(200);
            builder.Property(s => s.SignedFilePath).HasMaxLength(500);
            builder.Property(s => s.SignedFileHash).HasMaxLength(128);
            builder.Property(s => s.SignatureCertSubject).HasMaxLength(500);
            builder.Property(s => s.SigningIp).HasMaxLength(45);
            builder.Property(s => s.SigningUserAgent).HasMaxLength(500);
            builder.Property(s => s.ChallengeId).HasMaxLength(100);
            builder.Property(s => s.VerificationError).HasMaxLength(500);

            builder.HasOne(s => s.Lease)
                   .WithMany(l => l.Signatures)
                   .HasForeignKey(s => s.LeaseId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(s => new { s.LeaseId, s.UserId, s.Role }).IsUnique();
            builder.HasIndex(s => new { s.LeaseId, s.SequenceOrder }).IsUnique();
            builder.HasIndex(s => s.SignatureRef).IsUnique().HasFilter("\"SignatureRef\" IS NOT NULL");
            builder.HasIndex(s => new { s.LeaseId, s.SignedFileHash })
                   .IsUnique()
                   .HasFilter("\"SignedFileHash\" IS NOT NULL");
        });

        // ===== LeaseTermAcceptance (aceitação multi-parte) =====
        modelBuilder.Entity<LeaseTermAcceptance>(builder =>
        {
            builder.ToTable("LeaseTermAcceptances", "leasing");
            builder.HasKey(t => t.Id);
            builder.Property(t => t.Role).HasConversion<int>();
            builder.Property(t => t.AcceptedDocumentHash).HasMaxLength(128);
            builder.Property(t => t.IpAddress).HasMaxLength(45);
            builder.Property(t => t.UserAgent).HasMaxLength(500);

            builder.HasOne(t => t.Lease)
                   .WithMany(l => l.TermAcceptances)
                   .HasForeignKey(t => t.LeaseId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasIndex(t => new { t.LeaseId, t.UserId }).IsUnique();
        });
    }
}
