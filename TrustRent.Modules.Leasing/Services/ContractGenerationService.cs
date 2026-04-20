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
                        DrawTableRow(table, "Renovação:", "Automática por regra legal, salvo oposição válida nos prazos de pré-aviso.");
                        
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

                    // === COMUNICAÇÕES E NOTIFICAÇÕES ===
                    col.Item().PaddingTop(15).Element(c => SectionHeader(c, "VI. COMUNICAÇÕES E NOTIFICAÇÕES"));
                    col.Item().PaddingTop(5).Column(comms =>
                    {
                        DrawClause(comms, "1. Canal Oficial de Comunicações",
                            "As partes acordam que todas as comunicações, notificações e declarações relacionadas com o presente contrato — " +
                            "incluindo, mas não se limitando a, oposição à renovação, denúncia, atualização de renda, pedidos de obras e " +
                            "quaisquer outras declarações com relevância contratual ou legal — serão efetuadas preferencialmente " +
                            "através da plataforma eletrónica WeKaza e/ou dos endereços de correio eletrónico gerados pela mesma.");

                        DrawClause(comms, "2. Fundamentação Legal",
                            "Este acordo fundamenta-se nas seguintes disposições legais: " +
                            "(a) Artigo 9.º do Novo Regime do Arrendamento Urbano (NRAU, Lei n.º 6/2006, de 27 de fevereiro, " +
                            "com as alterações introduzidas pela Lei n.º 13/2019, de 12 de fevereiro), que permite que as comunicações " +
                            "entre senhorio e arrendatário sejam efetuadas por meios que assegurem a sua receção; " +
                            "(b) Artigo 224.º do Código Civil, que estabelece que a declaração negocial é eficaz logo que chega ao " +
                            "poder do destinatário ou é dele conhecida; " +
                            "(c) Artigo 3.º do Decreto-Lei n.º 290-D/99, de 2 de agosto (na redação dada pelo Decreto-Lei " +
                            "n.º 62/2003, de 3 de abril), que reconhece a validade e o valor probatório dos documentos eletrónicos; " +
                            "(d) Regulamento (UE) n.º 910/2014 (eIDAS), relativo à identificação eletrónica e aos serviços de confiança " +
                            "para transações eletrónicas no mercado interno.");

                        DrawClause(comms, "3. Valor Probatório",
                            "As comunicações realizadas através da plataforma são registadas automaticamente com data, hora (UTC), " +
                            "endereço IP do remetente e do destinatário no momento do envio e da visualização, constituindo prova " +
                            "bastante da efetivação da comunicação nos termos do artigo 224.º, n.º 2, do Código Civil. " +
                            "É gerado um hash criptográfico (SHA-256) do conteúdo de cada comunicação para garantir a sua integridade " +
                            "e inalterabilidade.");

                        DrawClause(comms, "4. Comunicação por Correio Eletrónico",
                            "Sem prejuízo das comunicações pela plataforma, estas poderão igualmente ser efetuadas por correio " +
                            "eletrónico para os endereços indicados pelas partes, considerando-se cumprida a obrigação de comunicação " +
                            "quando haja prova do envio pelo sistema, nos termos do artigo 10.º do NRAU.");

                        DrawClause(comms, "5. Comunicação Judicial ou Extrajudicial",
                            "Quando a lei exija forma específica de comunicação (nomeadamente carta registada com aviso de receção, " +
                            "conforme artigo 10.º do NRAU), as partes poderão utilizar cumulativamente a plataforma e o meio legalmente " +
                            "exigido, prevalecendo este último em caso de conflito.");
                    });

                    // === CONFIRMAÇÃO DE RECEÇÃO ===
                    col.Item().PaddingTop(15).Element(c => SectionHeader(c, "VII. CONFIRMAÇÃO DE RECEÇÃO DE COMUNICAÇÕES"));
                    col.Item().PaddingTop(5).Column(ack =>
                    {
                        DrawClause(ack, "1. Obrigação de Confirmação",
                            "As partes obrigam-se a confirmar a receção de qualquer comunicação oficial efetuada pela plataforma " +
                            "ou por correio eletrónico gerado pela mesma, num prazo máximo de 5 (cinco) dias úteis após a sua " +
                            "receção, utilizando para o efeito o mecanismo de confirmação disponibilizado pela plataforma.");

                        DrawClause(ack, "2. Presunção de Receção",
                            "A falta de confirmação de receção por parte do destinatário, dentro do prazo referido no número anterior, " +
                            "não invalida a comunicação nem afasta os seus efeitos legais, desde que o sistema registe prova " +
                            "do envio e/ou da disponibilização da comunicação ao destinatário, nos termos do artigo 224.º, n.º 2, " +
                            "do Código Civil — que estabelece que a declaração negocial é também eficaz quando só por culpa " +
                            "do destinatário não foi por ele oportunamente recebida.");

                        DrawClause(ack, "3. Registo de Confirmação",
                            "Cada confirmação de receção efetuada pela plataforma regista automaticamente a data, hora, " +
                            "endereço IP e identificação do utilizador, constituindo prova bastante nos termos do " +
                            "Decreto-Lei n.º 290-D/99.");

                        DrawClause(ack, "4. Efeitos da Notificação",
                            "Para todos os efeitos legais, incluindo o cômputo dos prazos de oposição à renovação, denúncia " +
                            "e demais comunicações previstas no NRAU (artigos 9.º a 15.º da Lei n.º 6/2006), as comunicações " +
                            "consideram-se eficazes: (a) na data da confirmação de receção pelo destinatário; ou " +
                            "(b) decorridos 5 (cinco) dias úteis após o envio pelo sistema, quando não haja confirmação, " +
                            "desde que exista prova de envio.");
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
