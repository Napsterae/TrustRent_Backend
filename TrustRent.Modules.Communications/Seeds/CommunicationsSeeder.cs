using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Communications.Contracts.Database;
using TrustRent.Modules.Communications.Models;

namespace TrustRent.Modules.Communications.Seeds;

public static class CommunicationsSeeder
{
    private static readonly Guid LandlordId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant2Id = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Landlord2Id = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Guid PendingApplicationId = Guid.Parse("cccc3333-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ContractPendingApplicationId = Guid.Parse("cccc7777-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid LeaseStartDateProposedApplicationId = Guid.Parse("cccc9999-cccc-cccc-cccc-cccccccccccc");

    private static readonly Guid Ticket1Id = Guid.Parse("dddd1111-dddd-dddd-dddd-dddddddddddd");
    private static readonly Guid Ticket3Id = Guid.Parse("dddd3333-dddd-dddd-dddd-dddddddddddd");

    private static readonly Guid SeedNotificationId = Guid.Parse("99991111-9999-1111-9999-111111111111");

    public static async Task SeedAsync(CommunicationsDbContext context)
    {
        if (await context.Notifications.AnyAsync(n => n.Id == SeedNotificationId))
        {
            var count = await context.Notifications.CountAsync();
            Console.WriteLine($"[SEED] Communications: Ja existem {count} notificacoes seedadas. A ignorar.");
            return;
        }

        var notifications = new List<Notification>
        {
            new()
            {
                Id = SeedNotificationId,
                UserId = LandlordId,
                Type = "application",
                Message = "Recebeste uma nova candidatura para o T2 Renovado no Chiado com Vista Rio.",
                ReferenceId = PendingApplicationId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddDays(-5)
            },
            new()
            {
                Id = Guid.Parse("99992222-9999-2222-9999-222222222222"),
                UserId = Tenant2Id,
                Type = "lease",
                Message = "O contrato do T2 em Alvalade foi gerado e aguarda assinatura do proprietario.",
                ReferenceId = ContractPendingApplicationId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = Guid.Parse("99993333-9999-3333-9999-333333333333"),
                UserId = TenantId,
                Type = "payment",
                Message = "O pagamento inicial do T1 em Braga esta pendente de confirmacao.",
                ReferenceId = Guid.Parse("cccc8888-cccc-cccc-cccc-cccccccccccc"),
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddDays(-2)
            },
            new()
            {
                Id = Guid.Parse("99994444-9999-4444-9999-444444444444"),
                UserId = LandlordId,
                Type = "ticket",
                Message = "Novo comentario no ticket de manutencao do ar condicionado.",
                ReferenceId = Ticket1Id,
                IsRead = true,
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = Guid.Parse("99995555-9999-5555-9999-555555555555"),
                UserId = Landlord2Id,
                Type = "lease",
                Message = "O inquilino sugeriu confirmar a data de inicio para o lease em Aveiro.",
                ReferenceId = LeaseStartDateProposedApplicationId,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-18)
            },
            new()
            {
                Id = Guid.Parse("99996666-9999-6666-9999-666666666666"),
                UserId = Landlord2Id,
                Type = "ticket",
                Message = "Foi aberto um ticket preventivo relativo a manutencao da piscina.",
                ReferenceId = Ticket3Id,
                IsRead = false,
                CreatedAt = DateTime.UtcNow.AddHours(-6)
            }
        };

        var messages = new List<Message>
        {
            new()
            {
                Id = Guid.Parse("88881111-8888-1111-8888-111111111111"),
                ContextId = PendingApplicationId,
                ContextType = MessageContextType.Application,
                SenderId = Tenant2Id,
                Content = "Boa tarde. Tenho flexibilidade para visitar na quinta ou no sabado.",
                CreatedAt = DateTime.UtcNow.AddDays(-5).AddHours(1)
            },
            new()
            {
                Id = Guid.Parse("88882222-8888-2222-8888-222222222222"),
                ContextId = PendingApplicationId,
                ContextType = MessageContextType.Application,
                SenderId = LandlordId,
                Content = "Obrigado pelo interesse. Estou a rever as disponibilidades e respondo hoje ao final do dia.",
                CreatedAt = DateTime.UtcNow.AddDays(-5).AddHours(6)
            },
            new()
            {
                Id = Guid.Parse("88883333-8888-3333-8888-333333333333"),
                ContextId = ContractPendingApplicationId,
                ContextType = MessageContextType.Application,
                SenderId = LandlordId,
                Content = "O contrato ja foi gerado. Assim que eu assinar, recebes notificacao para descarregar o PDF.",
                CreatedAt = DateTime.UtcNow.AddDays(-3)
            },
            new()
            {
                Id = Guid.Parse("88884444-8888-4444-8888-444444444444"),
                ContextId = ContractPendingApplicationId,
                ContextType = MessageContextType.Application,
                SenderId = Tenant2Id,
                Content = "Perfeito. Fico a aguardar para proceder logo que esteja disponivel.",
                CreatedAt = DateTime.UtcNow.AddDays(-3).AddHours(2)
            },
            new()
            {
                Id = Guid.Parse("88885555-8888-5555-8888-555555555555"),
                ContextId = LeaseStartDateProposedApplicationId,
                ContextType = MessageContextType.Application,
                SenderId = Tenant2Id,
                Content = "A data proposta funciona para mim, mas so consigo entrar ao fim do dia.",
                CreatedAt = DateTime.UtcNow.AddDays(-1)
            },
            new()
            {
                Id = Guid.Parse("88886666-8888-6666-8888-666666666666"),
                ContextId = LeaseStartDateProposedApplicationId,
                ContextType = MessageContextType.Application,
                SenderId = Landlord2Id,
                Content = "Sem problema. Se quiseres, fechamos o inventario no proprio dia da entrega de chaves.",
                CreatedAt = DateTime.UtcNow.AddDays(-1).AddHours(3)
            }
        };

        try
        {
            context.Notifications.AddRange(notifications);
            context.Messages.AddRange(messages);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Communications: {notifications.Count} notificacoes e {messages.Count} mensagens seedadas.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] Communications: Erro no Seed — {ex.InnerException?.Message ?? ex.Message}");
        }
    }
}