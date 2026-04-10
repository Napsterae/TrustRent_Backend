using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Diagnostics
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var services = new ServiceCollection();
            services.AddDbContext<CatalogDbContext>(options =>
                options.UseNpgsql("Host=localhost;Database=trustrent_db;Username=trustrent_admin;Password=TrustRentPassword123!"));

            var provider = services.BuildServiceProvider();
            using var scope = provider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<CatalogDbContext>();

            // Find the most recent lease
            var lease = await context.Leases
                .Include(l => l.History)
                .Include(l => l.Property)
                .OrderByDescending(l => l.CreatedAt)
                .FirstOrDefaultAsync();

            if (lease == null)
            {
                Console.WriteLine("No lease found in the DB.");
                return;
            }

            Console.WriteLine($"Found Lease: {lease.Id}, Status: {lease.Status}");

            var application = await context.Applications
                .Include(a => a.History)
                .FirstOrDefaultAsync(a => a.Id == lease.ApplicationId);

            if (application == null)
            {
                Console.WriteLine("Application not found.");
                return;
            }

            Console.WriteLine($"Found Application: {application.Id}, Status: {application.Status}");

            // Simulate the update
            lease.StartDate = DateTime.UtcNow.AddDays(1);
            lease.EndDate = lease.StartDate.AddMonths(12);
            lease.DurationMonths = 12;
            lease.UpdatedAt = DateTime.UtcNow;
            lease.Status = LeaseStatus.AwaitingSignatures;

            lease.History.Add(new LeaseHistory
            {
                Id = Guid.NewGuid(),
                LeaseId = lease.Id,
                ActorId = Guid.Empty,
                Action = "Test",
                Message = "Test"
            });

            application.Status = ApplicationStatus.ContractPendingSignature;
            application.UpdatedAt = DateTime.UtcNow;
            application.History.Add(new ApplicationHistory
            {
                Id = Guid.NewGuid(),
                ApplicationId = application.Id,
                ActorId = Guid.Empty,
                Action = "Test",
                Message = "Test"
            });

            try
            {
                await context.SaveChangesAsync();
                Console.WriteLine("SaveChanges succeeded!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SaveChanges failed: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner: {ex.InnerException.Message}");
                }
            }
        }
    }
}