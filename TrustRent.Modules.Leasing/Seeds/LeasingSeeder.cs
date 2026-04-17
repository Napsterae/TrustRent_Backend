using Microsoft.EntityFrameworkCore;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Seeds;

public static class LeasingSeeder
{
    // IDs fixos que devem corresponder aos IDs do IdentitySeeder e CatalogSeeder
    private static readonly Guid LandlordId  = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid TenantId    = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid Tenant2Id   = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Landlord2Id = Guid.Parse("44444444-4444-4444-4444-444444444444");

    private static readonly Guid Property5Id = Guid.Parse("aaaa5555-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Property6Id = Guid.Parse("aaaa6666-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Property7Id = Guid.Parse("aaaa7777-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Property8Id = Guid.Parse("aaaa8888-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid Property9Id = Guid.Parse("aaaa9999-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

    private static readonly Guid LeaseActiveApplication1Id = Guid.Parse("cccc1111-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid LeaseActiveApplication2Id = Guid.Parse("cccc2222-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid ContractPendingApplicationId = Guid.Parse("cccc7777-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid AwaitingPaymentApplicationId = Guid.Parse("cccc8888-cccc-cccc-cccc-cccccccccccc");
    private static readonly Guid LeaseStartDateProposedApplicationId = Guid.Parse("cccc9999-cccc-cccc-cccc-cccccccccccc");

    // IDs fixos para os leases core
    public static readonly Guid Lease1Id = Guid.Parse("bbbb1111-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid Lease2Id = Guid.Parse("bbbb2222-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid Lease3Id = Guid.Parse("bbbb3333-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid Lease4Id = Guid.Parse("bbbb4444-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    public static readonly Guid Lease5Id = Guid.Parse("bbbb5555-bbbb-bbbb-bbbb-bbbbbbbbbbbb");

    public static readonly Guid Ticket1Id = Guid.Parse("dddd1111-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid Ticket2Id = Guid.Parse("dddd2222-dddd-dddd-dddd-dddddddddddd");
    public static readonly Guid Ticket3Id = Guid.Parse("dddd3333-dddd-dddd-dddd-dddddddddddd");

    public static async Task SeedAsync(LeasingDbContext context)
    {
        if (await context.Leases.AnyAsync(l => l.Id == Lease1Id))
        {
            var count = await context.Leases.CountAsync();
            Console.WriteLine($"[SEED] Leasing: Já existem {count} contratos. A ignorar.");
            return;
        }

        QuestPDF.Settings.License = LicenseType.Community;

        var leases = new List<Lease>();
        var stripeAccounts = new List<StripeAccount>();
        var paymentMethods = new List<TenantPaymentMethod>();
        var payments = new List<Payment>();
        var tickets = new List<Ticket>();

        var contractsFolder = Path.Combine(AppContext.BaseDirectory, "storage", "seed-contracts");
        Directory.CreateDirectory(contractsFolder);

        // Lease 1: Carlos Mendes (Landlord) → Ana Ferreira (Tenant) — Property5 (Apartamento Luxo Torres Colombo)
        var lease1ContractPath = EnsureSeedPdf(
            contractsFolder,
            Lease1Id,
            "Contrato de Arrendamento Ativo",
            "Seed de contrato oficial com assinatura concluida.",
            new[]
            {
                "Imovel: Torres do Colombo",
                "Senhorio: Carlos Mendes",
                "Inquilina: Ana Ferreira",
                "Estado: Ativo"
            });

        var lease1 = new Lease
        {
            Id = Lease1Id,
            PropertyId = Property5Id,
            TenantId = TenantId,
            LandlordId = LandlordId,
            ApplicationId = LeaseActiveApplication1Id,
            StartDate = DateTime.UtcNow.AddMonths(-3),
            EndDate = DateTime.UtcNow.AddMonths(9),
            DurationMonths = 12,
            AllowsRenewal = true,
            MonthlyRent = 2500,
            Deposit = 5000,
            AdvanceRentMonths = 2,
            LeaseRegime = "PermanentHousing",
            ContractType = "Official",
            CondominiumFeesPaidBy = "Senhorio",
            WaterPaidBy = "Inquilino",
            ElectricityPaidBy = "Inquilino",
            GasPaidBy = "Inquilino",
            ContractFilePath = lease1ContractPath,
            ContractGeneratedAt = DateTime.UtcNow.AddMonths(-3).AddDays(-7),
            ContractSignedAt = DateTime.UtcNow.AddMonths(-3).AddDays(-4),
            LandlordSigned = true,
            LandlordSignedAt = DateTime.UtcNow.AddMonths(-3).AddDays(-5),
            LandlordSignatureRef = "CMD-LANDLORD-LEASE1",
            LandlordSignatureCertSubject = "CN=Carlos Mendes",
            TenantSigned = true,
            TenantSignedAt = DateTime.UtcNow.AddMonths(-3).AddDays(-4),
            TenantSignatureRef = "CMD-TENANT-LEASE1",
            TenantSignatureCertSubject = "CN=Ana Ferreira",
            LandlordSignatureVerified = true,
            TenantSignatureVerified = true,
            ContractFileHash = "seed-hash-contract-lease1",
            LandlordSignedFileHash = "seed-hash-landlord-lease1",
            Status = LeaseStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMonths(-3).AddDays(-5),
        };
        lease1.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease1Id, Action = "Contrato criado", ActorId = LandlordId, CreatedAt = lease1.CreatedAt });
        lease1.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease1Id, Action = "Contrato ativado", ActorId = LandlordId, CreatedAt = lease1.StartDate });
        leases.Add(lease1);

        // Lease 2: Sofia Rodrigues (Landlord2) → Miguel Costa (Tenant2) — Property6 (Moradia T2 Tavira)
        var lease2 = new Lease
        {
            Id = Lease2Id,
            PropertyId = Property6Id,
            TenantId = Tenant2Id,
            LandlordId = Landlord2Id,
            ApplicationId = LeaseActiveApplication2Id,
            StartDate = DateTime.UtcNow.AddMonths(-2),
            EndDate = DateTime.UtcNow.AddMonths(7),
            DurationMonths = 9,
            AllowsRenewal = false,
            MonthlyRent = 1100,
            Deposit = 1100,
            AdvanceRentMonths = 0,
            LeaseRegime = "NonPermanentHousing",
            ContractType = "Informal",
            CondominiumFeesPaidBy = "Nao aplicavel",
            WaterPaidBy = "Inquilino",
            ElectricityPaidBy = "Inquilino",
            GasPaidBy = "Inquilino",
            Status = LeaseStatus.Active,
            CreatedAt = DateTime.UtcNow.AddMonths(-2).AddDays(-3),
        };
        lease2.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease2Id, Action = "Contrato criado", ActorId = Landlord2Id, CreatedAt = lease2.CreatedAt });
        lease2.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease2Id, Action = "Contrato ativado", ActorId = Landlord2Id, CreatedAt = lease2.StartDate });
        leases.Add(lease2);

        var lease3ContractPath = EnsureSeedPdf(
            contractsFolder,
            Lease3Id,
            "Contrato Oficial Aguardando Assinatura",
            "Lease seedado para testar a fase ContractPendingSignature.",
            new[]
            {
                "Imovel: T2 Novo em Alvalade",
                "Senhorio: Carlos Mendes",
                "Inquilino: Miguel Costa",
                "Estado: PendingLandlordSignature"
            });

        var lease3 = new Lease
        {
            Id = Lease3Id,
            PropertyId = Property7Id,
            TenantId = Tenant2Id,
            LandlordId = LandlordId,
            ApplicationId = ContractPendingApplicationId,
            StartDate = DateTime.UtcNow.AddDays(18),
            EndDate = DateTime.UtcNow.AddDays(18).AddMonths(24),
            DurationMonths = 24,
            AllowsRenewal = true,
            MonthlyRent = 1450,
            Deposit = 1450,
            AdvanceRentMonths = 1,
            LeaseRegime = "PermanentHousing",
            ContractType = "Official",
            CondominiumFeesPaidBy = "Senhorio",
            WaterPaidBy = "Inquilino",
            ElectricityPaidBy = "Inquilino",
            GasPaidBy = "Inquilino",
            ContractFilePath = lease3ContractPath,
            ContractGeneratedAt = DateTime.UtcNow.AddDays(-4),
            ContractFileHash = "seed-hash-contract-lease3",
            Status = LeaseStatus.PendingLandlordSignature,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        lease3.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease3Id, Action = "LeaseInitiated", ActorId = LandlordId, Message = "Data proposta para inicio do arrendamento enviada ao inquilino.", CreatedAt = lease3.CreatedAt });
        lease3.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease3Id, Action = "StartDateConfirmed", ActorId = Tenant2Id, Message = "Data de inicio confirmada e contrato gerado.", CreatedAt = lease3.CreatedAt.AddDays(1) });
        leases.Add(lease3);

        var lease4 = new Lease
        {
            Id = Lease4Id,
            PropertyId = Property8Id,
            TenantId = TenantId,
            LandlordId = Landlord2Id,
            ApplicationId = AwaitingPaymentApplicationId,
            StartDate = DateTime.UtcNow.AddDays(10),
            EndDate = DateTime.UtcNow.AddDays(10).AddMonths(12),
            DurationMonths = 12,
            AllowsRenewal = true,
            MonthlyRent = 780,
            Deposit = 780,
            AdvanceRentMonths = 1,
            LeaseRegime = "PermanentHousing",
            ContractType = "Informal",
            CondominiumFeesPaidBy = "Senhorio",
            WaterPaidBy = "Inquilino",
            ElectricityPaidBy = "Inquilino",
            GasPaidBy = "Nao aplicavel",
            Status = LeaseStatus.AwaitingPayment,
            CreatedAt = DateTime.UtcNow.AddDays(-4)
        };
        lease4.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease4Id, Action = "LeaseInitiated", ActorId = Landlord2Id, Message = "Termos aceites e lease pronto para pagamento inicial.", CreatedAt = lease4.CreatedAt });
        leases.Add(lease4);

        var lease5 = new Lease
        {
            Id = Lease5Id,
            PropertyId = Property9Id,
            TenantId = Tenant2Id,
            LandlordId = Landlord2Id,
            ApplicationId = LeaseStartDateProposedApplicationId,
            StartDate = DateTime.UtcNow.AddDays(22),
            EndDate = DateTime.UtcNow.AddDays(22).AddMonths(12),
            DurationMonths = 12,
            AllowsRenewal = true,
            MonthlyRent = 980,
            Deposit = 980,
            AdvanceRentMonths = 0,
            LeaseRegime = "PermanentHousing",
            ContractType = "Official",
            CondominiumFeesPaidBy = "Senhorio",
            WaterPaidBy = "Inquilino",
            ElectricityPaidBy = "Inquilino",
            GasPaidBy = "Inquilino",
            Status = LeaseStatus.Pending,
            CreatedAt = DateTime.UtcNow.AddDays(-3)
        };
        lease5.History.Add(new LeaseHistory { Id = Guid.NewGuid(), LeaseId = Lease5Id, Action = "LeaseInitiated", ActorId = Landlord2Id, Message = "Data de inicio proposta e a aguardar confirmacao.", CreatedAt = lease5.CreatedAt });
        leases.Add(lease5);

        stripeAccounts.AddRange(new[]
        {
            new StripeAccount
            {
                Id = Guid.Parse("eeee1111-eeee-eeee-eeee-eeeeeeeeeeee"),
                UserId = LandlordId,
                PropertyId = null,
                StripeAccountId = "acct_seed_landlord_default",
                IsOnboardingComplete = true,
                ChargesEnabled = true,
                PayoutsEnabled = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-8)
            },
            new StripeAccount
            {
                Id = Guid.Parse("eeee2222-eeee-eeee-eeee-eeeeeeeeeeee"),
                UserId = Landlord2Id,
                PropertyId = null,
                StripeAccountId = "acct_seed_landlord2_default",
                IsOnboardingComplete = true,
                ChargesEnabled = true,
                PayoutsEnabled = true,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-7)
            },
            new StripeAccount
            {
                Id = Guid.Parse("eeee3333-eeee-eeee-eeee-eeeeeeeeeeee"),
                UserId = LandlordId,
                PropertyId = Property7Id,
                StripeAccountId = "acct_seed_landlord_property7",
                IsOnboardingComplete = true,
                ChargesEnabled = true,
                PayoutsEnabled = true,
                IsDefault = false,
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            }
        });

        paymentMethods.AddRange(new[]
        {
            new TenantPaymentMethod
            {
                Id = Guid.Parse("ffff1111-ffff-ffff-ffff-ffffffffffff"),
                UserId = TenantId,
                StripePaymentMethodId = "pm_seed_tenant_default",
                CardBrand = "visa",
                CardLast4 = "4242",
                CardExpMonth = 12,
                CardExpYear = DateTime.UtcNow.Year + 2,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-2)
            },
            new TenantPaymentMethod
            {
                Id = Guid.Parse("ffff2222-ffff-ffff-ffff-ffffffffffff"),
                UserId = Tenant2Id,
                StripePaymentMethodId = "pm_seed_tenant2_default",
                CardBrand = "mastercard",
                CardLast4 = "4444",
                CardExpMonth = 7,
                CardExpYear = DateTime.UtcNow.Year + 3,
                IsDefault = true,
                CreatedAt = DateTime.UtcNow.AddMonths(-1)
            }
        });

        payments.AddRange(new[]
        {
            new Payment
            {
                Id = Guid.Parse("1111aaaa-1111-aaaa-1111-aaaaaaaaaaaa"),
                LeaseId = Lease1Id,
                TenantId = TenantId,
                LandlordId = LandlordId,
                StripePaymentIntentId = "pi_seed_lease1_initial",
                Type = PaymentType.InitialPayment,
                Amount = 12500m,
                PlatformFee = 90m,
                LandlordAmount = 12410m,
                RentAmount = 2500m,
                DepositAmount = 5000m,
                AdvanceRentAmount = 5000m,
                Currency = "eur",
                Status = PaymentStatus.Succeeded,
                PaidAt = DateTime.UtcNow.AddMonths(-3).AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-3).AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddMonths(-3).AddDays(-1)
            },
            new Payment
            {
                Id = Guid.Parse("2222aaaa-2222-aaaa-2222-aaaaaaaaaaaa"),
                LeaseId = Lease1Id,
                TenantId = TenantId,
                LandlordId = LandlordId,
                StripePaymentIntentId = "pi_seed_lease1_month_1",
                Type = PaymentType.MonthlyRent,
                Amount = 2500m,
                PlatformFee = 30m,
                LandlordAmount = 2470m,
                RentAmount = 2500m,
                DepositAmount = 0m,
                AdvanceRentAmount = 0m,
                Currency = "eur",
                Status = PaymentStatus.Succeeded,
                PaidAt = DateTime.UtcNow.AddMonths(-2),
                CreatedAt = DateTime.UtcNow.AddMonths(-2).AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddMonths(-2)
            },
            new Payment
            {
                Id = Guid.Parse("3333aaaa-3333-aaaa-3333-aaaaaaaaaaaa"),
                LeaseId = Lease1Id,
                TenantId = TenantId,
                LandlordId = LandlordId,
                StripePaymentIntentId = "pi_seed_lease1_month_2",
                Type = PaymentType.MonthlyRent,
                Amount = 2500m,
                PlatformFee = 30m,
                LandlordAmount = 2470m,
                RentAmount = 2500m,
                DepositAmount = 0m,
                AdvanceRentAmount = 0m,
                Currency = "eur",
                Status = PaymentStatus.Succeeded,
                PaidAt = DateTime.UtcNow.AddMonths(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1).AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddMonths(-1)
            },
            new Payment
            {
                Id = Guid.Parse("4444aaaa-4444-aaaa-4444-aaaaaaaaaaaa"),
                LeaseId = Lease2Id,
                TenantId = Tenant2Id,
                LandlordId = Landlord2Id,
                StripePaymentIntentId = "pi_seed_lease2_initial",
                Type = PaymentType.InitialPayment,
                Amount = 2200m,
                PlatformFee = 30m,
                LandlordAmount = 2170m,
                RentAmount = 1100m,
                DepositAmount = 1100m,
                AdvanceRentAmount = 0m,
                Currency = "eur",
                Status = PaymentStatus.Succeeded,
                PaidAt = DateTime.UtcNow.AddMonths(-2).AddDays(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-2).AddDays(-2),
                UpdatedAt = DateTime.UtcNow.AddMonths(-2).AddDays(-1)
            },
            new Payment
            {
                Id = Guid.Parse("5555aaaa-5555-aaaa-5555-aaaaaaaaaaaa"),
                LeaseId = Lease2Id,
                TenantId = Tenant2Id,
                LandlordId = Landlord2Id,
                StripePaymentIntentId = "pi_seed_lease2_month_1",
                Type = PaymentType.MonthlyRent,
                Amount = 1100m,
                PlatformFee = 30m,
                LandlordAmount = 1070m,
                RentAmount = 1100m,
                DepositAmount = 0m,
                AdvanceRentAmount = 0m,
                Currency = "eur",
                Status = PaymentStatus.Succeeded,
                PaidAt = DateTime.UtcNow.AddMonths(-1),
                CreatedAt = DateTime.UtcNow.AddMonths(-1).AddDays(-1),
                UpdatedAt = DateTime.UtcNow.AddMonths(-1)
            }
        });

        var ticket1 = new Ticket
        {
            Id = Ticket1Id,
            LeaseId = Lease1Id,
            TenantId = TenantId,
            LandlordId = LandlordId,
            Title = "Ar condicionado da sala necessita manutencao",
            Description = "O equipamento deixou de refrescar corretamente e faz ruido ao fim de alguns minutos.",
            Priority = TicketPriority.High,
            Status = TicketStatus.InProgress,
            CreatedAt = DateTime.UtcNow.AddDays(-12),
            UpdatedAt = DateTime.UtcNow.AddDays(-2)
        };
        ticket1.Comments.Add(new TicketComment { Id = Guid.NewGuid(), TicketId = Ticket1Id, AuthorId = TenantId, Content = "Consigo enviar video do problema se for util.", CreatedAt = ticket1.CreatedAt.AddHours(2) });
        ticket1.Comments.Add(new TicketComment { Id = Guid.NewGuid(), TicketId = Ticket1Id, AuthorId = LandlordId, Content = "Ja agendei tecnico para amanha ao final da tarde.", CreatedAt = ticket1.CreatedAt.AddDays(1) });
        ticket1.Attachments.Add(new TicketAttachment { Id = Guid.NewGuid(), TicketId = Ticket1Id, StorageUrl = "https://example.com/seed/ac-video.mp4", FileName = "ac-video.mp4", UploadedAt = ticket1.CreatedAt.AddHours(3) });
        tickets.Add(ticket1);

        var ticket2 = new Ticket
        {
            Id = Ticket2Id,
            LeaseId = Lease1Id,
            TenantId = TenantId,
            LandlordId = LandlordId,
            Title = "Ajuste na pressao da agua do duche",
            Description = "A pressao esta mais baixa do que o normal desde o fim de semana.",
            Priority = TicketPriority.Medium,
            Status = TicketStatus.Resolved,
            CreatedAt = DateTime.UtcNow.AddDays(-20),
            UpdatedAt = DateTime.UtcNow.AddDays(-17),
            ResolvedAt = DateTime.UtcNow.AddDays(-17)
        };
        ticket2.Comments.Add(new TicketComment { Id = Guid.NewGuid(), TicketId = Ticket2Id, AuthorId = LandlordId, Content = "O canalizador ja regularizou a valvula principal.", CreatedAt = ticket2.CreatedAt.AddDays(2) });
        tickets.Add(ticket2);

        var ticket3 = new Ticket
        {
            Id = Ticket3Id,
            LeaseId = Lease2Id,
            TenantId = Tenant2Id,
            LandlordId = Landlord2Id,
            Title = "Limpeza preventiva da piscina",
            Description = "Queria validar a periodicidade da manutencao da piscina antes do Verao.",
            Priority = TicketPriority.Low,
            Status = TicketStatus.Open,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };
        ticket3.Comments.Add(new TicketComment { Id = Guid.NewGuid(), TicketId = Ticket3Id, AuthorId = Tenant2Id, Content = "Nao ha urgencia, e apenas planeamento preventivo.", CreatedAt = ticket3.CreatedAt.AddHours(1) });
        tickets.Add(ticket3);

        try
        {
            context.Leases.AddRange(leases);
            context.StripeAccounts.AddRange(stripeAccounts);
            context.TenantPaymentMethods.AddRange(paymentMethods);
            context.Payments.AddRange(payments);
            context.Tickets.AddRange(tickets);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Leasing: {leases.Count} contratos, {payments.Count} pagamentos, {tickets.Count} tickets e dados Stripe de teste criados.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SEED] Leasing: Erro no Seed — {ex.InnerException?.Message ?? ex.Message}");
        }
    }

    private static string EnsureSeedPdf(string folderPath, Guid leaseId, string title, string subtitle, IEnumerable<string> lines)
    {
        var filePath = Path.Combine(folderPath, $"contract_{leaseId}.pdf");
        if (File.Exists(filePath))
            return filePath;

        Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Margin(40);
                page.Size(PageSizes.A4);
                page.Content().Column(column =>
                {
                    column.Spacing(12);
                    column.Item().Text("TrustRent").FontSize(24).SemiBold();
                    column.Item().Text(title).FontSize(18).Bold();
                    column.Item().Text(subtitle).FontSize(12).FontColor(Colors.Grey.Darken2);
                    foreach (var line in lines)
                    {
                        column.Item().Text(line).FontSize(12);
                    }
                    column.Item().PaddingTop(20).Text("Documento placeholder gerado pelas seeds de desenvolvimento.").Italic();
                });
            });
        }).GeneratePdf(filePath);

        return filePath;
    }
}
