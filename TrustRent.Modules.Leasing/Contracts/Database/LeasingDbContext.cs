using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Leasing.Contracts.Database;

public class LeasingDbContext : DbContext
{
    public LeasingDbContext(DbContextOptions<LeasingDbContext> options) : base(options) { }

    public DbSet<Ticket> Tickets { get; set; }
    public DbSet<TicketComment> TicketComments { get; set; }
    public DbSet<TicketAttachment> TicketAttachments { get; set; }

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
    }
}
