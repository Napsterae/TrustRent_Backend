using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Communications.Models;

namespace TrustRent.Modules.Communications.Contracts.Database;

public class CommunicationsDbContext : DbContext
{
    public CommunicationsDbContext(DbContextOptions<CommunicationsDbContext> options) : base(options) { }

    public DbSet<Message> Messages { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("communications");

        modelBuilder.Entity<Message>(builder =>
        {
            builder.HasKey(m => m.Id);
            builder.HasIndex(m => m.ContextId); // Index para procurar mensagens rapidamente por ApplicationId / TicketId
        });

        base.OnModelCreating(modelBuilder);
    }
}
