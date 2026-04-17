using System.Globalization;
using Microsoft.Extensions.Configuration;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;

namespace TrustRent.Modules.Leasing.Services;

public class ContractGenerationService : IContractGenerationService
{
    private readonly string _storagePath;

    public ContractGenerationService(IConfiguration configuration)
    {
        _storagePath = configuration["Storage:ContractPath"] ?? "./storage/leases";
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public async Task<string> GenerateContractPdfAsync(
        Lease lease,
        string landlordName, string landlordNif, string landlordAddress,
        string tenantName, string tenantNif, string tenantAddress,
        ContractPropertyInfo propertyInfo)
    {
        var leaseDir = Path.Combine(_storagePath, lease.Id.ToString());
        Directory.CreateDirectory(leaseDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmss");
        var fileName = $"contract_{timestamp}.pdf";
        var filePath = Path.Combine(leaseDir, fileName);

        var ptCulture = new CultureInfo("pt-PT");

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(2, Unit.Centimetre);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica").FontColor(Colors.Black));

                // ── HEADER ──
                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("CONTRATO DE ARRENDAMENTO HABITACIONAL")
                                    .FontSize(16).Bold().FontColor("#1e3a8a");
                                c.Item().Text(lease.ContractType == "Official" ? "Com Prazo Certo" : "Informal / Proposta")
                                    .FontSize(10).FontColor(Colors.Grey.Darken1);
                            });
                            row.ConstantItem(100).AlignRight().Text("WeKaza")
                                .FontSize(18).Bold().FontColor("#ea580c");
                        });
                        col.Item().PaddingTop(10).LineHorizontal(1.5f).LineColor("#e5e7eb");
                    });
                });

                // ── CONTENT ──
                page.Content().PaddingVertical(15).Column(col =>
                {
                    // === IDENTIFICAÇÃO DAS PARTES ===
                    col.Item().Element(c => SectionHeader(c, "I. IDENTIFICAÇÃO DAS PARTES"));
                    
                    col.Item().PaddingTop(5).Background("#f8fafc").Border(1).BorderColor("#e2e8f0").Padding(10).Column(partes =>
                    {
                        partes.Item().Text("PRIMEIRO OUTORGANTE (Senhorio):").Bold().FontColor("#334155");
                        partes.Item().Text(t =>
                        {
                            t.Span("Nome: ").SemiBold(); t.Span($"{landlordName}\n");
                            t.Span("NIF: ").SemiBold(); t.Span($"{landlordNif}\n");
                            t.Span("Morada: ").SemiBold(); t.Span(landlordAddress);
                        });

                        partes.Item().PaddingTop(10).Text("SEGUNDO OUTORGANTE (Arrendatário):").Bold().FontColor("#334155");
                        partes.Item().Text(t =>
                        {
                            t.Span("Nome: ").SemiBold(); t.Span($"{tenantName}\n");
                            t.Span("NIF: ").SemiBold(); t.Span($"{tenantNif}\n");
                            t.Span("Morada: ").SemiBold(); t.Span(tenantAddress);
                        });
                    });

                    // === IDENTIFICAÇÃO DO IMÓVEL ===
                    col.Item().PaddingTop(15).Element(c => SectionHeader(c, "II. IDENTIFICAÇÃO DO IMÓVEL"));
                    
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
                            columns.RelativeColumn();
                        });

                        DrawTableRow(table, "Morada:", $"{propertyInfo.Street}, {propertyInfo.DoorNumber}");
                        DrawTableRow(table, "Localidade:", $"{propertyInfo.PostalCode} {propertyInfo.Parish}, {propertyInfo.Municipality}, {propertyInfo.District}");
                        DrawTableRow(table, "Tipologia:", propertyInfo.Typology ?? "");
                        
                        if (!string.IsNullOrEmpty(propertyInfo.MatrixArticle))
                            DrawTableRow(table, "Artigo Matricial:", $"{propertyInfo.MatrixArticle} " + (!string.IsNullOrEmpty(propertyInfo.PropertyFraction) ? $"(Fração: {propertyInfo.PropertyFraction})" : ""));
                        
                        if (!string.IsNullOrEmpty(propertyInfo.UsageLicenseNumber))
                            DrawTableRow(table, "Licença Utilização:", $"{propertyInfo.UsageLicenseNumber} emitida em {propertyInfo.UsageLicenseDate} por {propertyInfo.UsageLicenseIssuer}");
                            
                        if (!string.IsNullOrEmpty(propertyInfo.EnergyCertificateNumber))
                            DrawTableRow(table, "Certificado Energético:", $"{propertyInfo.EnergyCertificateNumber} (Classe {propertyInfo.EnergyClass})");
                    });

                    // === CONDIÇÕES FINANCEIRAS E PRAZOS ===
                    col.Item().PaddingTop(15).Element(c => SectionHeader(c, "III. CONDIÇÕES FINANCEIRAS E PRAZOS"));

                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.ConstantColumn(120);
                            columns.RelativeColumn();
                        });

                        DrawTableRow(table, "Renda Mensal:", lease.MonthlyRent.ToString("C2", ptCulture), true);
                        if (lease.Deposit.HasValue)
                            DrawTableRow(table, "Caução de Garantia:", lease.Deposit.Value.ToString("C2", ptCulture));
                        if (lease.AdvanceRentMonths > 0)
                            DrawTableRow(table, "Rendas Antecipadas:", $"{lease.AdvanceRentMonths} {(lease.AdvanceRentMonths == 1 ? "mês" : "meses")} ({(lease.MonthlyRent * lease.AdvanceRentMonths).ToString("C2", ptCulture)})");
                            
                        DrawTableRow(table, "Data de Início:", lease.StartDate.ToString("dd 'de' MMMM 'de' yyyy", ptCulture));
                        DrawTableRow(table, "Duração Inicial:", $"{lease.DurationMonths} meses (Termo: {lease.EndDate.ToString("dd/MM/yyyy", ptCulture)})");
                        DrawTableRow(table, "Renovação:", lease.AllowsRenewal ? "Automática por iguais períodos" : "Não renovável");
                        
                        if (!string.IsNullOrEmpty(lease.LeaseRegime))
                            DrawTableRow(table, "Regime Legal:", lease.LeaseRegime == "PermanentHousing" ? "Habitação Permanente" : "Habitação Não Permanente");
                    });

                    // === DESPESAS E ENCARGOS ===
                    col.Item().PaddingTop(15).Element(c => SectionHeader(c, "IV. DESPESAS E ENCARGOS"));
                    col.Item().PaddingTop(5).Text("Salvo acordo escrito em contrário, os encargos e despesas associados ao imóvel distribuem-se da seguinte forma:").FontSize(9).FontColor("#475569");
                    
                    col.Item().PaddingTop(5).Table(table =>
                    {
                        table.ColumnsDefinition(columns =>
                        {
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                            columns.RelativeColumn();
                        });

                        table.Header(header =>
                        {
                            header.Cell().BorderBottom(1).BorderColor("#cbd5e1").PaddingBottom(2).Text("Condomínio").Bold();
                            header.Cell().BorderBottom(1).BorderColor("#cbd5e1").PaddingBottom(2).Text("Água").Bold();
                            header.Cell().BorderBottom(1).BorderColor("#cbd5e1").PaddingBottom(2).Text("Eletricidade").Bold();
                            header.Cell().BorderBottom(1).BorderColor("#cbd5e1").PaddingBottom(2).Text("Gás / Net").Bold();
                        });

                        table.Cell().PaddingTop(2).Text(lease.CondominiumFeesPaidBy);
                        table.Cell().PaddingTop(2).Text(lease.WaterPaidBy);
                        table.Cell().PaddingTop(2).Text(lease.ElectricityPaidBy);
                        table.Cell().PaddingTop(2).Text(lease.GasPaidBy);
                    });

                    // === CLÁUSULAS LEGAIS ===
                    col.Item().PaddingTop(15).Element(c => SectionHeader(c, "V. CLÁUSULAS GERAIS E CONDIÇÕES"));
                    col.Item().PaddingTop(5).Column(clausulas =>
                    {
                        DrawClause(clausulas, "1. Finalidade", "O local arrendado destina-se exclusivamente a habitação do Segundo Outorgante, não lhe podendo ser dado outro fim, nem subarrendar, ceder ou emprestar, no todo ou em parte, sem consentimento expresso e escrito do Primeiro Outorgante.");
                        DrawClause(clausulas, "2. Pagamento da Renda", "A renda mensal deverá ser paga até ao dia 8 do mês anterior àquele a que disser respeito, através de transferência bancária para o IBAN a indicar pelo Primeiro Outorgante.");
                        DrawClause(clausulas, "3. Conservação", "O Segundo Outorgante reconhece que o imóvel lhe é entregue em bom estado de conservação, com todas as instalações em perfeito funcionamento, obrigando-se a mantê-las e a restituir o imóvel no mesmo estado findo o contrato, ressalvado o desgaste proveniente da sua prudente utilização.");
                        if (lease.Deposit.HasValue)
                            DrawClause(clausulas, "4. Caução", $"A caução no valor de {lease.Deposit.Value.ToString("C2", ptCulture)} destina-se a garantir a reparação de eventuais danos causados no imóvel ou incumprimento de obrigações contratuais, sendo restituída no termo do contrato caso não se verifiquem tais situações.");
                        if (lease.AdvanceRentMonths > 0)
                            DrawClause(clausulas, "4-A. Rendas Antecipadas", $"No início do contrato, o Segundo Outorgante entregará {lease.AdvanceRentMonths} {(lease.AdvanceRentMonths == 1 ? "renda antecipada" : "rendas antecipadas")}, no valor global de {(lease.MonthlyRent * lease.AdvanceRentMonths).ToString("C2", ptCulture)}, a imputar ao pagamento dos últimos {lease.AdvanceRentMonths} {(lease.AdvanceRentMonths == 1 ? "mês" : "meses")} do contrato, salvo acordo escrito em contrário.");
                        DrawClause(clausulas, "5. Benfeitorias", "O Segundo Outorgante não poderá realizar obras ou benfeitorias sem autorização prévia e por escrito do Primeiro Outorgante. As obras autorizadas ficarão a fazer parte integrante do imóvel, sem direito a qualquer retenção ou indemnização.");
                        DrawClause(clausulas, "6. Rescisão", "A denúncia ou oposição à renovação do contrato por qualquer das partes obedece aos prazos e formas previstos na lei em vigor (NRAU).");
                        DrawClause(clausulas, "7. Foro Competente", "Para dirimir quaisquer litígios emergentes da interpretação ou execução deste contrato, as partes estipulam como competente o foro da comarca onde se situa o imóvel.");
                    });

                    // === ASSINATURAS ===
                    col.Item().PaddingTop(30).Row(row =>
                    {
                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("O PRIMEIRO OUTORGANTE").Bold().FontColor("#1e3a8a");
                            c.Item().PaddingTop(40).LineHorizontal(1).LineColor("#94a3b8");
                            c.Item().Text(landlordName).FontSize(9);
                            c.Item().Text($"Data: {(lease.LandlordSignedAt.HasValue ? lease.LandlordSignedAt.Value.ToString("dd/MM/yyyy") : "___/___/______")}").FontSize(9);
                            if (!string.IsNullOrEmpty(lease.LandlordSignatureRef))
                            {
                                c.Item().Row(r => {
                                    r.AutoItem().PaddingRight(4).Text("Assinatura CMD:").FontSize(8).Bold().FontColor("#10b981");
                                    r.RelativeItem().Text(lease.LandlordSignatureRef).FontSize(8).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });

                        row.ConstantItem(40);

                        row.RelativeItem().Column(c =>
                        {
                            c.Item().Text("O SEGUNDO OUTORGANTE").Bold().FontColor("#1e3a8a");
                            c.Item().PaddingTop(40).LineHorizontal(1).LineColor("#94a3b8");
                            c.Item().Text(tenantName).FontSize(9);
                            c.Item().Text($"Data: {(lease.TenantSignedAt.HasValue ? lease.TenantSignedAt.Value.ToString("dd/MM/yyyy") : "___/___/______")}").FontSize(9);
                            if (!string.IsNullOrEmpty(lease.TenantSignatureRef))
                            {
                                c.Item().Row(r => {
                                    r.AutoItem().PaddingRight(4).Text("Assinatura CMD:").FontSize(8).Bold().FontColor("#10b981");
                                    r.RelativeItem().Text(lease.TenantSignatureRef).FontSize(8).FontColor(Colors.Grey.Darken1);
                                });
                            }
                        });
                    });
                });

                // ── FOOTER ──
                page.Footer().Column(f =>
                {
                    f.Item().LineHorizontal(0.5f).LineColor("#cbd5e1");
                    f.Item().PaddingTop(4).Row(row =>
                    {
                        row.RelativeItem().Text($"Documento gerado automaticamente pelo WeKaza em {DateTime.UtcNow.ToString("dd/MM/yyyy HH:mm 'UTC'", ptCulture)}")
                            .FontSize(7).FontColor(Colors.Grey.Medium);
                        
                        row.RelativeItem().AlignRight().Text(text =>
                        {
                            text.Span("Página ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.CurrentPageNumber().FontSize(7).FontColor(Colors.Grey.Medium);
                            text.Span(" de ").FontSize(7).FontColor(Colors.Grey.Medium);
                            text.TotalPages().FontSize(7).FontColor(Colors.Grey.Medium);
                        });
                    });
                    f.Item().Text($"Contrato ID: {lease.Id}").FontSize(7).FontColor(Colors.Grey.Lighten1);
                });
            });
        });

        document.GeneratePdf(filePath);

        return await Task.FromResult(filePath);
    }

    public async Task<byte[]> GetContractBytesAsync(string contractFilePath)
    {
        if (!File.Exists(contractFilePath))
            throw new FileNotFoundException("Ficheiro do contrato não encontrado.", contractFilePath);

        return await File.ReadAllBytesAsync(contractFilePath);
    }

    private static void SectionHeader(IContainer container, string title)
    {
        container.Background("#e0f2fe").PaddingVertical(4).PaddingHorizontal(8)
                 .Text(title).Bold().FontSize(11).FontColor("#0f172a");
    }

    private static void DrawTableRow(TableDescriptor table, string label, string value, bool isBoldValue = false)
    {
        table.Cell().BorderBottom(1).BorderColor("#f1f5f9").PaddingVertical(3).Text(label).SemiBold().FontColor("#475569").FontSize(9);
        var textCell = table.Cell().BorderBottom(1).BorderColor("#f1f5f9").PaddingVertical(3).Text(value).FontSize(9).FontColor("#0f172a");
        if (isBoldValue) textCell.Bold();
    }

    private static void DrawClause(ColumnDescriptor column, string title, string content)
    {
        column.Item().PaddingBottom(6).Text(text =>
        {
            text.Span($"{title} - ").Bold().FontSize(9).FontColor("#334155");
            text.Span(content).FontSize(9).FontColor("#475569");
        });
    }
}
