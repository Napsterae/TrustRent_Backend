using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Contracts.Database;

public class AdminDbContext : DbContext
{
    public AdminDbContext(DbContextOptions<AdminDbContext> options) : base(options) { }

    public DbSet<AdminUser> AdminUsers => Set<AdminUser>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<AdminRole> Roles => Set<AdminRole>();
    public DbSet<AdminRolePermission> RolePermissions => Set<AdminRolePermission>();
    public DbSet<AdminUserRole> UserRoles => Set<AdminUserRole>();
    public DbSet<AdminUserPermission> UserPermissions => Set<AdminUserPermission>();
    public DbSet<AdminSession> Sessions => Set<AdminSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<PlatformSetting> PlatformSettings => Set<PlatformSetting>();
    public DbSet<FeatureFlag> FeatureFlags => Set<FeatureFlag>();
    public DbSet<SupportTicket> SupportTickets => Set<SupportTicket>();
    public DbSet<SupportTicketMessage> SupportTicketMessages => Set<SupportTicketMessage>();
    public DbSet<PaymentOperation> PaymentOperations => Set<PaymentOperation>();
    public DbSet<ConsentRecord> ConsentRecords => Set<ConsentRecord>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("admin");

        modelBuilder.Entity<AdminUser>(b =>
        {
            b.ToTable("AdminUsers");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Email).IsUnique();
            b.Property(x => x.Email).IsRequired().HasMaxLength(256);
            b.Property(x => x.Name).IsRequired().HasMaxLength(200);
            b.Property(x => x.PasswordHash).IsRequired().HasMaxLength(500);
            b.Property(x => x.MfaSecret).HasMaxLength(200);
            b.Property(x => x.MfaRecoveryCodesHash);
            b.Property(x => x.PasswordResetTokenHash).HasMaxLength(300);
            b.Property(x => x.LastLoginIp).HasMaxLength(64);
        });

        modelBuilder.Entity<Permission>(b =>
        {
            b.ToTable("Permissions");
            b.HasKey(x => x.Code);
            b.Property(x => x.Code).HasMaxLength(80);
            b.Property(x => x.Description).HasMaxLength(300);
            b.Property(x => x.Category).HasMaxLength(80);
        });

        modelBuilder.Entity<AdminRole>(b =>
        {
            b.ToTable("AdminRoles");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.Name).IsUnique();
            b.Property(x => x.Name).IsRequired().HasMaxLength(100);
            b.Property(x => x.Description).HasMaxLength(300);
        });

        modelBuilder.Entity<AdminRolePermission>(b =>
        {
            b.ToTable("AdminRolePermissions");
            b.HasKey(x => new { x.RoleId, x.PermissionCode });
            b.HasOne(x => x.Role).WithMany(r => r.Permissions).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionCode).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminUserRole>(b =>
        {
            b.ToTable("AdminUserRoles");
            b.HasKey(x => new { x.AdminUserId, x.RoleId });
            b.HasOne(x => x.AdminUser).WithMany(u => u.Roles).HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Role).WithMany(r => r.Members).HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminUserPermission>(b =>
        {
            b.ToTable("AdminUserPermissions");
            b.HasKey(x => new { x.AdminUserId, x.PermissionCode });
            b.HasOne(x => x.AdminUser).WithMany(u => u.PermissionOverrides).HasForeignKey(x => x.AdminUserId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Permission).WithMany().HasForeignKey(x => x.PermissionCode).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AdminSession>(b =>
        {
            b.ToTable("AdminSessions");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.TokenId).IsUnique();
            b.Property(x => x.TokenId).IsRequired().HasMaxLength(100);
            b.Property(x => x.RevokedReason).HasMaxLength(300);
            b.Property(x => x.Ip).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(300);
        });

        modelBuilder.Entity<AuditLog>(b =>
        {
            b.ToTable("AuditLog");
            b.HasKey(x => x.Id);
            b.Property(x => x.Action).IsRequired().HasMaxLength(120);
            b.Property(x => x.EntityType).IsRequired().HasMaxLength(80);
            b.Property(x => x.EntityId).HasMaxLength(80);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.Ip).HasMaxLength(64);
            b.Property(x => x.UserAgent).HasMaxLength(300);
            b.Property(x => x.CorrelationId).HasMaxLength(80);
            b.HasIndex(x => x.CreatedAt);
            b.HasIndex(x => x.AdminUserId);
            b.HasIndex(x => new { x.EntityType, x.EntityId });
        });

        modelBuilder.Entity<PlatformSetting>(b =>
        {
            b.ToTable("PlatformSettings");
            b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(120);
            b.Property(x => x.Category).HasMaxLength(80);
            b.Property(x => x.Description).HasMaxLength(500);
            b.Property(x => x.ValueType).HasMaxLength(20);
        });

        modelBuilder.Entity<FeatureFlag>(b =>
        {
            b.ToTable("FeatureFlags");
            b.HasKey(x => x.Key);
            b.Property(x => x.Key).HasMaxLength(120);
            b.Property(x => x.Description).HasMaxLength(500);
        });

        modelBuilder.Entity<SupportTicket>(b =>
        {
            b.ToTable("SupportTickets");
            b.HasKey(x => x.Id);
            b.Property(x => x.Subject).IsRequired().HasMaxLength(300);
            b.Property(x => x.Category).HasMaxLength(80);
            b.Property(x => x.SourceChannel).IsRequired().HasMaxLength(80);
            b.Property(x => x.PagePath).HasMaxLength(500);
            b.Property(x => x.PageUrl).HasMaxLength(1200);
            b.Property(x => x.ClientBrowser).HasMaxLength(160);
            b.Property(x => x.ClientOs).HasMaxLength(160);
            b.Property(x => x.ClientDevice).HasMaxLength(160);
            b.Property(x => x.ArchivedByAdminId);
            b.Property(x => x.MetadataJson).HasColumnType("jsonb");
            b.HasIndex(x => x.IsArchived);
            b.HasIndex(x => x.OpenedByUserId);
            b.HasIndex(x => x.AssignedAdminId);
            b.HasIndex(x => x.Kind);
            b.HasIndex(x => x.PagePath);
            b.HasIndex(x => x.State);
        });

        modelBuilder.Entity<SupportTicketMessage>(b =>
        {
            b.ToTable("SupportTicketMessages");
            b.HasKey(x => x.Id);
            b.Property(x => x.Body).IsRequired();
            b.HasOne(x => x.Ticket).WithMany(t => t.Messages).HasForeignKey(x => x.TicketId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<PaymentOperation>(b =>
        {
            b.ToTable("PaymentOperations");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.IdempotencyKey).IsUnique();
            b.Property(x => x.IdempotencyKey).IsRequired().HasMaxLength(200);
            b.Property(x => x.Currency).HasMaxLength(10);
            b.Property(x => x.Reason).HasMaxLength(500);
            b.Property(x => x.StripeObjectId).HasMaxLength(200);
            b.Property(x => x.Amount).HasColumnType("numeric(18,2)");
        });

        modelBuilder.Entity<ConsentRecord>(b =>
        {
            b.ToTable("ConsentRecords");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.OpenedByUserId);
            b.HasIndex(x => x.SessionId);
            b.HasIndex(x => x.CreatedAt).IsDescending();
            b.Property(x => x.ConsentStatus).IsRequired().HasMaxLength(20);
            b.Property(x => x.Mechanism).IsRequired().HasMaxLength(50);
            b.Property(x => x.PurposesJson).HasColumnType("jsonb");
            b.Property(x => x.BannerVersion).HasMaxLength(50);
            b.Property(x => x.PrivacyPolicyVersion).HasMaxLength(50);
            b.Property(x => x.SourceUrl).HasMaxLength(500);
            b.Property(x => x.Language).HasMaxLength(10);
            b.Property(x => x.UserAgent).HasMaxLength(500);
            b.Property(x => x.IpAddress).HasMaxLength(128);
        });

        modelBuilder.Entity<WebhookEvent>(b =>
        {
            b.ToTable("WebhookEvents");
            b.HasKey(x => x.Id);
            b.HasIndex(x => x.StripeEventId).IsUnique();
            b.HasIndex(x => x.EventType);
            b.HasIndex(x => x.CreatedAt);
            b.Property(x => x.StripeEventId).IsRequired().HasMaxLength(200);
            b.Property(x => x.EventType).IsRequired().HasMaxLength(100);
            b.Property(x => x.Status).IsRequired().HasMaxLength(20);
            b.Property(x => x.Error).HasMaxLength(2000);
            b.Property(x => x.PayloadSummary).HasMaxLength(500);
        });

        base.OnModelCreating(modelBuilder);
    }
}
