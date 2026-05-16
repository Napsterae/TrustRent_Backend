using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Communications.Models;

namespace TrustRent.Modules.Communications.Contracts.Database;

public class CommunicationsDbContext : DbContext
{
    public CommunicationsDbContext(DbContextOptions<CommunicationsDbContext> options) : base(options) { }

    public DbSet<Message> Messages { get; set; }
    public DbSet<Notification> Notifications { get; set; }
    public DbSet<PushDevice> PushDevices { get; set; }
    public DbSet<Broadcast> Broadcasts => Set<Broadcast>();
    public DbSet<EmailTemplate> EmailTemplates => Set<EmailTemplate>();
    public DbSet<LegalDocumentVersion> LegalDocumentVersions => Set<LegalDocumentVersion>();
    public DbSet<Banner> Banners => Set<Banner>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("communications");

        modelBuilder.Entity<Message>(builder =>
        {
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => m.ContextId);
        });

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.HasKey(n => n.Id);
            builder.HasIndex(n => n.UserId);
            builder.HasIndex(n => new { n.UserId, n.IsRead });
        });

        modelBuilder.Entity<PushDevice>(builder =>
        {
            builder.HasKey(d => d.Id);
            builder.HasIndex(d => d.UserId);
            builder.HasIndex(d => d.ExpoPushToken).IsUnique();
            builder.HasIndex(d => new { d.UserId, d.IsActive });
        });

        modelBuilder.Entity<Broadcast>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Audience).HasMaxLength(40);
            b.Property(x => x.Channel).HasMaxLength(20);
            b.Property(x => x.Status).HasMaxLength(20);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.ScheduledAt);
        });

        modelBuilder.Entity<EmailTemplate>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Key).HasMaxLength(100).IsRequired();
            b.Property(x => x.Name).HasMaxLength(120).IsRequired();
            b.Property(x => x.Version).HasMaxLength(32).IsRequired();
            b.HasIndex(x => new { x.Key, x.Locale, x.Version }).IsUnique();
            b.HasIndex(x => new { x.Key, x.Locale, x.IsActive });
            b.Property(x => x.Subject).HasMaxLength(300).IsRequired();
            b.Property(x => x.Locale).HasMaxLength(10);
        });

        modelBuilder.Entity<LegalDocumentVersion>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.DocumentType).HasMaxLength(40).IsRequired();
            b.Property(x => x.Version).HasMaxLength(32).IsRequired();
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Summary).HasMaxLength(500);
            b.Property(x => x.ChangeSummary).HasMaxLength(2000);
            b.HasIndex(x => new { x.DocumentType, x.Version }).IsUnique();
            b.HasIndex(x => new { x.DocumentType, x.IsCurrent });
            b.HasIndex(x => new { x.DocumentType, x.PublishedAt });
        });

        modelBuilder.Entity<Banner>(b =>
        {
            b.HasKey(x => x.Id);
            b.Property(x => x.Title).HasMaxLength(200).IsRequired();
            b.Property(x => x.Severity).HasMaxLength(20);
            b.Property(x => x.Audience).HasMaxLength(40);
            b.HasIndex(x => new { x.IsActive, x.StartsAt, x.EndsAt });
        });

        base.OnModelCreating(modelBuilder);
    }
}
