using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Communications.Models;

namespace TrustRent.Modules.Communications.Contracts.Database;

public class CommunicationsDbContext : DbContext
{
    public CommunicationsDbContext(DbContextOptions<CommunicationsDbContext> options) : base(options) { }

    public DbSet<Message> Messages { get; set; }
    public DbSet<Notification> Notifications { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("communications");

        modelBuilder.Entity<Message>(builder =>
        {
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => m.ContextId); // Index para procurar mensagens rapidamente por ApplicationId / TicketId
        });

        modelBuilder.Entity<Notification>(builder =>
        {
            builder.HasKey(n => n.Id);
            builder.HasIndex(n => n.UserId); // Index para queries frequentes de utilizador
            builder.HasIndex(n => new { n.UserId, n.IsRead }); // Index para contagem de não-lidas
        });

        base.OnModelCreating(modelBuilder);
    }
}
