using System.Text;

namespace TrustRent.Shared.Communications;

public sealed record CommunicationVariableDefinition(
    string Key,
    string Label,
    string Description,
    string Category,
    bool IsConfigurable = false,
    string? SettingKey = null,
    string? ExampleValue = null,
    IReadOnlyList<string>? AppliesToKeys = null);

public sealed record EmailTemplateSeedDefinition(
    string Key,
    string DisplayName,
    string Category,
    string Description,
    string DefaultVersion,
    string DefaultName,
    string SubjectTemplate,
    string BodyHtmlTemplate,
    string? BodyTextTemplate,
    IReadOnlyList<string> SupportedVariables);

public sealed record LegalDocumentSeedDefinition(
    string DocumentType,
    string Title,
    string DefaultVersion,
    string Summary,
    string ChangeSummary,
    string BodyHtmlTemplate,
    string? BodyTextTemplate,
    IReadOnlyList<string> SupportedVariables);

public static class CommunicationEmailTemplateKeys
{
    public const string AuthLoginCode = "auth.login_code";
    public const string ApplicationSubmitted = "applications.submitted";
    public const string ApplicationUpdated = "applications.updated";
    public const string ApplicationNewMessage = "applications.new_message";
    public const string ApplicationCoTenantInvite = "applications.co_tenant_invite";
    public const string ApplicationGuarantorInvite = "applications.guarantor_invite";
    public const string ApplicationGuarantorApproved = "applications.guarantor_approved";
    public const string ApplicationGuarantorRejected = "applications.guarantor_rejected";
    public const string ReviewPending = "reviews.pending_review";
    public const string LeaseSignaturePending = "leases.signature_pending";
    public const string LeaseGuarantorContractStarted = "leases.guarantor_contract_started";
    public const string LeaseGuarantorContractUpdated = "leases.guarantor_contract_updated";
    public const string LeaseGuarantorSignaturePending = "leases.guarantor_signature_pending";
    public const string PaymentInitialRequired = "payments.initial_required";
    public const string PaymentSucceeded = "payments.succeeded";
    public const string PaymentFailed = "payments.failed";
    public const string PaymentDepositRefunded = "payments.deposit_refunded";
    public const string LeaseNonRenewalNotice = "leases.non_renewal_notice";
    public const string LeaseEarlyTerminationNotice = "leases.early_termination_notice";
    public const string LeaseRentIncreaseNotice = "leases.rent_increase_notice";
    public const string LeaseRentIncreaseContestation = "leases.rent_increase_contestation";
    public const string LegalDocumentUpdated = "legal.document_updated";

    public static readonly IReadOnlyList<string> All =
    [
        AuthLoginCode,
        ApplicationSubmitted,
        ApplicationUpdated,
        ApplicationNewMessage,
        ApplicationCoTenantInvite,
        ApplicationGuarantorInvite,
        ApplicationGuarantorApproved,
        ApplicationGuarantorRejected,
        ReviewPending,
        LeaseSignaturePending,
        LeaseGuarantorContractStarted,
        LeaseGuarantorContractUpdated,
        LeaseGuarantorSignaturePending,
        PaymentInitialRequired,
        PaymentSucceeded,
        PaymentFailed,
        PaymentDepositRefunded,
        LeaseNonRenewalNotice,
        LeaseEarlyTerminationNotice,
        LeaseRentIncreaseNotice,
        LeaseRentIncreaseContestation,
        LegalDocumentUpdated
    ];
}

public static class CommunicationCatalog
{
    public static readonly IReadOnlyList<CommunicationVariableDefinition> Variables =
    [
        new("AppName", "Nome da aplicação", "Nome comercial apresentado nos documentos e emails.", "global", true, "branding.app_name", "Wekaza"),
        new("CompanyName", "Nome da empresa", "Entidade legal responsável pela plataforma.", "global", true, "branding.company_name", "Wekaza, Lda."),
        new("CompanyAddress", "Morada da empresa", "Morada usada em documentos e contactos legais.", "global", true, "branding.company_address", "Lisboa, Portugal"),
        new("SupportEmail", "Email de suporte", "Contacto de suporte ao utilizador.", "global", true, "branding.support_email", "suporte@wekaza.pt"),
        new("SupportPhone", "Telefone de suporte", "Telefone de apoio ao cliente.", "global", true, "branding.support_phone", "+351 210 000 000"),
        new("WebsiteUrl", "Website", "URL pública principal da marca.", "global", true, "branding.website_url", "https://wekaza.pt"),
        new("FrontendBaseUrl", "Frontend base URL", "Base URL usada para gerar links públicos.", "global", true, "branding.frontend_base_url", "https://app.wekaza.pt"),
        new("PrivacyPolicyUrl", "URL da política de privacidade", "Link público para a política de privacidade.", "global", true, "branding.privacy_policy_url", "https://app.wekaza.pt/politica-de-privacidade"),
        new("TermsOfUseUrl", "URL dos termos de utilização", "Link público para os termos de utilização.", "global", true, "branding.terms_of_use_url", "https://app.wekaza.pt/termos-de-utilizacao"),
        new("CurrentYear", "Ano atual", "Ano corrente, útil em rodapés e documentos.", "global", false, null, "2026"),
        new("LoginCode", "Código de login", "Código one-time enviado no fluxo de autenticação.", "auth", false, null, "123456", [CommunicationEmailTemplateKeys.AuthLoginCode]),
        new("LoginCodeExpiresMinutes", "Minutos até expirar", "Tempo de validade do código de login.", "auth", false, null, "15", [CommunicationEmailTemplateKeys.AuthLoginCode]),
        new("ApplicantName", "Nome do candidato", "Utilizador que submeteu a candidatura.", "applications", false, null, "Miguel Costa", [CommunicationEmailTemplateKeys.ApplicationSubmitted]),
        new("InviterName", "Nome do convidador", "Utilizador que iniciou o convite.", "applications", false, null, "Ana Ferreira", [CommunicationEmailTemplateKeys.ApplicationCoTenantInvite, CommunicationEmailTemplateKeys.ApplicationGuarantorInvite]),
        new("PropertyTitle", "Título do imóvel", "Título comercial do imóvel associado ao fluxo.", "applications", false, null, "T2 renovado no Chiado", [CommunicationEmailTemplateKeys.ApplicationSubmitted, CommunicationEmailTemplateKeys.ApplicationUpdated, CommunicationEmailTemplateKeys.ApplicationNewMessage, CommunicationEmailTemplateKeys.ApplicationCoTenantInvite, CommunicationEmailTemplateKeys.ApplicationGuarantorInvite, CommunicationEmailTemplateKeys.LeaseSignaturePending, CommunicationEmailTemplateKeys.PaymentInitialRequired, CommunicationEmailTemplateKeys.PaymentSucceeded, CommunicationEmailTemplateKeys.PaymentFailed, CommunicationEmailTemplateKeys.PaymentDepositRefunded]),
        new("ApplicationMessage", "Mensagem da candidatura", "Mensagem inicial deixada pelo candidato ao submeter a candidatura.", "applications", false, null, "Procuro entrada já no próximo mês e tenho documentação pronta.", [CommunicationEmailTemplateKeys.ApplicationSubmitted]),
        new("ApplicationStatusLabel", "Estado da candidatura", "Estado legível da candidatura após uma atualização.", "applications", false, null, "Visita aceite", [CommunicationEmailTemplateKeys.ApplicationUpdated]),
        new("UpdateMessage", "Resumo da atualização", "Resumo textual da alteração mais recente na candidatura.", "applications", false, null, "O senhorio aceitou a tua data de visita.", [CommunicationEmailTemplateKeys.ApplicationUpdated]),
        new("ApplicationUrl", "URL da candidatura", "Link autenticado para abrir a candidatura no frontend web.", "applications", false, null, "https://app.wekaza.pt/applications/123456", [CommunicationEmailTemplateKeys.ApplicationSubmitted, CommunicationEmailTemplateKeys.ApplicationUpdated, CommunicationEmailTemplateKeys.ApplicationNewMessage]),
        new("SenderName", "Nome do remetente", "Utilizador que enviou a mensagem na candidatura.", "applications", false, null, "João Costa", [CommunicationEmailTemplateKeys.ApplicationNewMessage]),
        new("MessagePreview", "Pré-visualização da mensagem", "Excerto da mensagem enviada na conversa da candidatura.", "applications", false, null, "Olá, consigo fazer a visita amanhã ao final do dia.", [CommunicationEmailTemplateKeys.ApplicationNewMessage]),
        new("GuestAccessUrl", "URL segura do convidado", "Link com token para área segura do convidado/fiador.", "applications", false, null, "https://app.wekaza.pt/guarantor/guest/token", [CommunicationEmailTemplateKeys.ApplicationGuarantorInvite, CommunicationEmailTemplateKeys.ApplicationGuarantorApproved, CommunicationEmailTemplateKeys.ApplicationGuarantorRejected, CommunicationEmailTemplateKeys.LeaseGuarantorContractStarted, CommunicationEmailTemplateKeys.LeaseGuarantorContractUpdated, CommunicationEmailTemplateKeys.LeaseGuarantorSignaturePending]),
        new("CounterpartyName", "Nome da contraparte", "Pessoa que deve ser avaliada no fluxo pendente.", "reviews", false, null, "Rita Almeida", [CommunicationEmailTemplateKeys.ReviewPending]),
        new("ReviewContextLabel", "Contexto da avaliação", "Tipo de fluxo a que a avaliação diz respeito.", "reviews", false, null, "arrendamento", [CommunicationEmailTemplateKeys.ReviewPending]),
        new("ReviewDeadline", "Prazo da avaliação", "Data limite para submeter a avaliação.", "reviews", false, null, "25/05/2026", [CommunicationEmailTemplateKeys.ReviewPending]),
        new("ReviewUrl", "URL das avaliações", "Link autenticado para a área onde as avaliações pendentes são resolvidas.", "reviews", false, null, "https://app.wekaza.pt/reviews", [CommunicationEmailTemplateKeys.ReviewPending]),
        new("MessageBody", "Mensagem do evento", "Mensagem dinâmica incluída no email do fluxo.", "leases", false, null, "Existe uma atualização relevante no contrato.", [CommunicationEmailTemplateKeys.LeaseSignaturePending, CommunicationEmailTemplateKeys.LeaseGuarantorContractStarted, CommunicationEmailTemplateKeys.LeaseGuarantorContractUpdated, CommunicationEmailTemplateKeys.LeaseGuarantorSignaturePending]),
        new("LeaseUrl", "URL do contrato", "Link autenticado para acompanhar o contrato e as assinaturas.", "leases", false, null, "https://app.wekaza.pt/contracts", [CommunicationEmailTemplateKeys.LeaseSignaturePending, CommunicationEmailTemplateKeys.PaymentInitialRequired, CommunicationEmailTemplateKeys.PaymentSucceeded, CommunicationEmailTemplateKeys.PaymentFailed, CommunicationEmailTemplateKeys.PaymentDepositRefunded]),
        new("SignerRoleLabel", "Papel da assinatura", "Parte do contrato cuja ação é esperada.", "leases", false, null, "inquilino", [CommunicationEmailTemplateKeys.LeaseSignaturePending]),
        new("LeaseReference", "Referência do contrato", "Identificador curto do contrato.", "leases", false, null, "ABC12345", [CommunicationEmailTemplateKeys.LeaseNonRenewalNotice, CommunicationEmailTemplateKeys.LeaseEarlyTerminationNotice, CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice, CommunicationEmailTemplateKeys.LeaseRentIncreaseContestation]),
        new("DecisionActor", "Parte decisora", "Parte que tomou a decisão comunicada no fluxo legal.", "leases", false, null, "senhorio", [CommunicationEmailTemplateKeys.LeaseNonRenewalNotice]),
        new("LeaseEndDate", "Data de término", "Data de fim do contrato.", "leases", false, null, "31/12/2026", [CommunicationEmailTemplateKeys.LeaseNonRenewalNotice]),
        new("ProposedTerminationDate", "Data proposta de saída", "Data proposta para cessação do contrato.", "leases", false, null, "30/09/2026", [CommunicationEmailTemplateKeys.LeaseEarlyTerminationNotice]),
        new("NoticeDays", "Dias de pré-aviso", "Dias de pré-aviso aplicáveis ao ato legal.", "leases", false, null, "120", [CommunicationEmailTemplateKeys.LeaseEarlyTerminationNotice]),
        new("IndemnificationAmount", "Valor de indemnização", "Montante de indemnização associado ao pré-aviso insuficiente.", "leases", false, null, "350,00€", [CommunicationEmailTemplateKeys.LeaseEarlyTerminationNotice]),
        new("Reason", "Motivo", "Motivo textual comunicado no fluxo legal.", "leases", false, null, "Mudança profissional", [CommunicationEmailTemplateKeys.LeaseEarlyTerminationNotice, CommunicationEmailTemplateKeys.LeaseRentIncreaseContestation]),
        new("PaymentAmount", "Valor do pagamento", "Montante associado ao evento de pagamento.", "payments", false, null, "1 450,00€", [CommunicationEmailTemplateKeys.PaymentInitialRequired, CommunicationEmailTemplateKeys.PaymentSucceeded, CommunicationEmailTemplateKeys.PaymentFailed, CommunicationEmailTemplateKeys.PaymentDepositRefunded]),
        new("PaymentTypeLabel", "Tipo de pagamento", "Descrição do tipo de pagamento associado ao evento.", "payments", false, null, "pagamento inicial", [CommunicationEmailTemplateKeys.PaymentInitialRequired, CommunicationEmailTemplateKeys.PaymentSucceeded, CommunicationEmailTemplateKeys.PaymentFailed, CommunicationEmailTemplateKeys.PaymentDepositRefunded]),
        new("PaymentStatusLabel", "Estado do pagamento", "Estado legível do fluxo de pagamento.", "payments", false, null, "confirmado", [CommunicationEmailTemplateKeys.PaymentInitialRequired, CommunicationEmailTemplateKeys.PaymentSucceeded, CommunicationEmailTemplateKeys.PaymentFailed, CommunicationEmailTemplateKeys.PaymentDepositRefunded]),
        new("FailureReason", "Motivo da falha", "Descrição da falha devolvida pelo processador de pagamentos.", "payments", false, null, "O método de pagamento foi recusado.", [CommunicationEmailTemplateKeys.PaymentFailed]),
        new("CurrentRent", "Renda atual", "Valor atual da renda.", "leases", false, null, "950,00€", [CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice, CommunicationEmailTemplateKeys.LeaseRentIncreaseContestation]),
        new("NewRent", "Nova renda", "Valor proposto após atualização.", "leases", false, null, "980,00€", [CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice, CommunicationEmailTemplateKeys.LeaseRentIncreaseContestation]),
        new("IncreasePercentage", "Percentagem de aumento", "Percentagem aplicada à atualização da renda.", "leases", false, null, "3,12%", [CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice]),
        new("CoefficientApplied", "Coeficiente aplicado", "Coeficiente legal aplicado ao aumento da renda.", "leases", false, null, "1,0312", [CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice]),
        new("EffectiveDate", "Data de entrada em vigor", "Data em que a alteração passa a vigorar.", "leases", false, null, "01/10/2026", [CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice]),
        new("ContestationDeadline", "Prazo de contestação", "Data limite para contestar a atualização.", "leases", false, null, "31/08/2026", [CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice]),
        new("DocumentTitle", "Título do documento", "Nome do documento legal alterado.", "legal", false, null, "Política de privacidade", [CommunicationEmailTemplateKeys.LegalDocumentUpdated]),
        new("DocumentTypeLabel", "Tipo do documento", "Tipo legível do documento legal.", "legal", false, null, "Política de privacidade", [CommunicationEmailTemplateKeys.LegalDocumentUpdated]),
        new("DocumentVersion", "Versão do documento", "Versão publicada do documento legal.", "legal", false, null, "1.0.1", [CommunicationEmailTemplateKeys.LegalDocumentUpdated]),
        new("DocumentUrl", "URL do documento", "Link público para a versão publicada.", "legal", false, null, "https://app.wekaza.pt/politica-de-privacidade?version=1.0.1", [CommunicationEmailTemplateKeys.LegalDocumentUpdated])
    ];

    public static readonly IReadOnlyList<EmailTemplateSeedDefinition> EmailTemplates =
    [
        new(
            CommunicationEmailTemplateKeys.AuthLoginCode,
            "Código de login",
            "Autenticação",
            "Entrega do código one-time para autenticação por email.",
            "1.0.2",
            "Default 2026",
            $"O teu código de acesso — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#374151">Usa o código abaixo para entrar na tua conta {Token("AppName")}.</p>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 24px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                <tr>
                    <td align="center" bgcolor="#349f99" style="padding:20px 16px;background-color:#349f99;background-image:linear-gradient(135deg,#a65710 0%,#f2a04b 18%,#1e6b66 58%,#41b0a8 100%);border-radius:22px">
                        <p style="margin:0 0 10px;font-size:12px;line-height:1.4;letter-spacing:2px;text-transform:uppercase;font-weight:700;color:#fff1df">Código de acesso</p>
                        <p style="margin:0;font-family:'Courier New',Courier,monospace;font-size:34px;line-height:1.1;font-weight:700;letter-spacing:6px;color:#ffffff;white-space:nowrap;-webkit-user-select:all;user-select:all">{Token("LoginCode")}</p>
                    </td>
                </tr>
            </table>
            <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 18px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                <tr>
                    <td style="padding:12px 18px;border:1px solid #d7e3e2;border-radius:999px;background-color:#f8fbfb;font-size:13px;line-height:1.5;font-weight:600;color:#1e6b66">Para copiar rapidamente, seleciona o código acima. A maioria dos clientes de email não suporta cópia com um clique.</td>
                </tr>
            </table>
            <p style="margin:0 0 14px;font-size:15px;line-height:1.7;color:#475569">Este código expira em <strong>{Token("LoginCodeExpiresMinutes")} minutos</strong> e só pode ser usado uma vez.</p>
            <p style="margin:0;font-size:14px;line-height:1.6;color:#64748b">Se não pediste este acesso, podes ignorar este email.</p>
            """,
            $"Usa o código {Token("LoginCode")} para entrar em {Token("AppName")}. Seleciona e copia o código manualmente se precisares. Expira em {Token("LoginCodeExpiresMinutes")} minutos.",
            ["AppName", "LoginCode", "LoginCodeExpiresMinutes"]),
        new(
            CommunicationEmailTemplateKeys.ApplicationSubmitted,
            "Nova candidatura",
            "Candidaturas",
            "Email enviado ao senhorio quando entra uma nova candidatura.",
            "1.0.0",
            "Default 2026",
            $"Nova candidatura para {Token("PropertyTitle")} — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155"><strong>{Token("ApplicantName")}</strong> submeteu uma nova candidatura ao imóvel <strong>{Token("PropertyTitle")}</strong>.</p>
            {AccentPanel("Mensagem do candidato", Token("ApplicationMessage"))}
            <p style="margin:0 0 18px;font-size:14px;line-height:1.7;color:#475569">Abre a candidatura para rever o pedido, responder ao candidato e gerir os próximos passos.</p>
            {ActionButton("ApplicationUrl", "Abrir candidatura")}
            {FallbackLink("ApplicationUrl")}
            """,
            $"{Token("ApplicantName")} submeteu uma candidatura a {Token("PropertyTitle")}. Consulta em {Token("ApplicationUrl")}",
            ["AppName", "ApplicantName", "PropertyTitle", "ApplicationMessage", "ApplicationUrl"]),
        new(
            CommunicationEmailTemplateKeys.ApplicationUpdated,
            "Atualização de candidatura",
            "Candidaturas",
            "Email enviado quando o estado ou o próximo passo de uma candidatura muda.",
            "1.0.0",
            "Default 2026",
            $"Atualização da candidatura — {Token("PropertyTitle")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">Há uma nova atualização na candidatura ao imóvel <strong>{Token("PropertyTitle")}</strong>.</p>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 22px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                <tr>
                    <td style="padding:18px 20px;background-color:#f7f3ee;background-image:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);border:1px solid #e5d6c6;border-radius:20px">
                        <p style="margin:0 0 8px;font-size:12px;line-height:1.4;letter-spacing:1.6px;text-transform:uppercase;font-weight:700;color:#1e6b66">Estado atual</p>
                        <p style="margin:0 0 10px;font-size:16px;line-height:1.6;color:#0f172a;font-weight:700">{Token("ApplicationStatusLabel")}</p>
                        <p style="margin:0;font-size:14px;line-height:1.7;color:#475569">{Token("UpdateMessage")}</p>
                    </td>
                </tr>
            </table>
            {ActionButton("ApplicationUrl", "Consultar candidatura")}
            {FallbackLink("ApplicationUrl")}
            """,
            $"Atualização na candidatura a {Token("PropertyTitle")}: {Token("ApplicationStatusLabel")}. {Token("UpdateMessage")} {Token("ApplicationUrl")}",
            ["PropertyTitle", "ApplicationStatusLabel", "UpdateMessage", "ApplicationUrl"]),
        new(
            CommunicationEmailTemplateKeys.ApplicationNewMessage,
            "Nova mensagem na candidatura",
            "Candidaturas",
            "Email enviado quando chega uma nova mensagem na conversa da candidatura.",
            "1.0.0",
            "Default 2026",
            $"Nova mensagem na candidatura — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155"><strong>{Token("SenderName")}</strong> enviou uma nova mensagem na candidatura do imóvel <strong>{Token("PropertyTitle")}</strong>.</p>
            {AccentPanel("Pré-visualização", Token("MessagePreview"))}
            {ActionButton("ApplicationUrl", "Abrir conversa")}
            {FallbackLink("ApplicationUrl")}
            """,
            $"{Token("SenderName")} enviou uma nova mensagem na candidatura a {Token("PropertyTitle")}: {Token("MessagePreview")} {Token("ApplicationUrl")}",
            ["AppName", "SenderName", "PropertyTitle", "MessagePreview", "ApplicationUrl"]),
        new(
            CommunicationEmailTemplateKeys.ApplicationCoTenantInvite,
            "Convite de co-candidato",
            "Candidaturas",
            "Convite enviado a um co-candidato numa candidatura conjunta.",
            "1.0.0",
            "Default 2026",
            $"Convite para candidatura conjunta — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">{Token("InviterName")} convidou-te para co-candidatar ao imóvel <strong>{Token("PropertyTitle")}</strong>.</p>
            {AccentPanel("Próximo passo", "Entra na plataforma, revê os detalhes da candidatura e aceita ou recusa o convite no teu painel.")}
            <p style="margin:0;font-size:14px;line-height:1.7;color:#475569">Se já tens conta, encontrarás o convite na tua área pessoal.</p>
            """,
            $"{Token("InviterName")} convidou-te para a candidatura ao imóvel {Token("PropertyTitle")}. Consulta o teu painel em {Token("AppName")}.",
            ["AppName", "InviterName", "PropertyTitle"]),
        new(
            CommunicationEmailTemplateKeys.ApplicationGuarantorInvite,
            "Convite de fiador",
            "Candidaturas",
            "Convite enviado a um fiador com acesso seguro por token.",
            "1.0.0",
            "Default 2026",
            $"Convite para fiador — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">{Token("InviterName")} indicou-te como fiador para a candidatura ao imóvel <strong>{Token("PropertyTitle")}</strong>.</p>
            {AccentPanel("Próximo passo", "Acede de forma segura, confirma os dados da candidatura e submete a tua informação e documentos.")}
            {ActionButton("GuestAccessUrl", "Abrir convite")}
            {FallbackLink("GuestAccessUrl")}
            """,
            $"{Token("InviterName")} convidou-te como fiador para {Token("PropertyTitle")}. Abre {Token("GuestAccessUrl")}.",
            ["AppName", "InviterName", "PropertyTitle", "GuestAccessUrl"]),
        new(
            CommunicationEmailTemplateKeys.ApplicationGuarantorApproved,
            "Fiador aprovado",
            "Candidaturas",
            "Comunicação enviada ao fiador quando a validação é aprovada.",
            "1.0.0",
            "Default 2026",
            $"Fiador aprovado — {Token("AppName")}",
            StatusEmailHtml("Fiador aprovado", "O senhorio aprovou os teus dados de fiador. Avisamos-te novamente quando o contrato estiver pronto para assinatura."),
            $"Os teus dados de fiador foram aprovados. Consulta a tua área segura em {Token("GuestAccessUrl")}.",
            ["AppName", "GuestAccessUrl"]),
        new(
            CommunicationEmailTemplateKeys.ApplicationGuarantorRejected,
            "Fiador não aprovado",
            "Candidaturas",
            "Comunicação enviada ao fiador quando a validação é rejeitada.",
            "1.0.0",
            "Default 2026",
            $"Fiador não aprovado — {Token("AppName")}",
            StatusEmailHtml("Fiador não aprovado", "O senhorio não aprovou a proposta de fiador para esta candidatura."),
            $"A proposta de fiador não foi aprovada. Consulta a tua área segura em {Token("GuestAccessUrl")}.",
            ["AppName", "GuestAccessUrl"]),
        new(
            CommunicationEmailTemplateKeys.ReviewPending,
            "Avaliação pendente",
            "Avaliações",
            "Lembrete enviado quando existe uma avaliação pendente por submeter.",
            "1.0.0",
            "Default 2026",
            $"Tens uma avaliação pendente — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">Tens uma avaliação pendente relativa ao teu <strong>{Token("ReviewContextLabel")}</strong> com <strong>{Token("CounterpartyName")}</strong>.</p>
            {AccentPanel("Prazo limite", $"Submete a avaliação até <strong>{Token("ReviewDeadline")}</strong> para que a publicação aconteça dentro do prazo previsto.")}
            {ActionButton("ReviewUrl", "Abrir avaliações")}
            {FallbackLink("ReviewUrl")}
            """,
            $"Tens uma avaliação pendente relativa ao teu {Token("ReviewContextLabel")} com {Token("CounterpartyName")}. Prazo: {Token("ReviewDeadline")}. {Token("ReviewUrl")}",
            ["AppName", "CounterpartyName", "ReviewContextLabel", "ReviewDeadline", "ReviewUrl"]),
        new(
            CommunicationEmailTemplateKeys.LeaseSignaturePending,
            "Assinatura pendente",
            "Arrendamentos",
            "Email enviado à próxima parte que precisa de assinar ou aceitar o contrato.",
            "1.0.0",
            "Default 2026",
            $"A tua assinatura é necessária — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">Chegou a vez de <strong>{Token("SignerRoleLabel")}</strong> concluir o próximo passo do contrato associado ao imóvel <strong>{Token("PropertyTitle")}</strong>.</p>
            {AccentPanel("Próximo passo", Token("MessageBody"))}
            {ActionButton("LeaseUrl", "Abrir contrato")}
            {FallbackLink("LeaseUrl")}
            """,
            $"É necessária a assinatura de {Token("SignerRoleLabel")} para o contrato de {Token("PropertyTitle")}. {Token("MessageBody")} {Token("LeaseUrl")}",
            ["AppName", "SignerRoleLabel", "PropertyTitle", "MessageBody", "LeaseUrl"]),
        new(
            CommunicationEmailTemplateKeys.LeaseGuarantorContractStarted,
            "Fiador: contrato iniciado",
            "Arrendamentos",
            "Aviso enviado ao fiador convidado quando o contrato arranca.",
            "1.0.0",
            "Default 2026",
            $"Contrato iniciado — {Token("AppName")}",
            GuestLeaseEmailHtml("Contrato iniciado", Token("MessageBody")),
            $"{Token("MessageBody")} Abre {Token("GuestAccessUrl")}.",
            ["AppName", "GuestAccessUrl", "MessageBody"]),
        new(
            CommunicationEmailTemplateKeys.LeaseGuarantorContractUpdated,
            "Fiador: atualização do contrato",
            "Arrendamentos",
            "Aviso enviado ao fiador convidado quando há um avanço no contrato.",
            "1.0.0",
            "Default 2026",
            $"Atualização do contrato — {Token("AppName")}",
            GuestLeaseEmailHtml("Atualização do contrato", Token("MessageBody")),
            $"{Token("MessageBody")} Abre {Token("GuestAccessUrl")}.",
            ["AppName", "GuestAccessUrl", "MessageBody"]),
        new(
            CommunicationEmailTemplateKeys.LeaseGuarantorSignaturePending,
            "Fiador: assinatura pendente",
            "Arrendamentos",
            "Aviso enviado ao fiador convidado quando a sua ação é necessária.",
            "1.0.0",
            "Default 2026",
            $"Assinatura pendente — {Token("AppName")}",
            GuestLeaseEmailHtml("Assinatura pendente", Token("MessageBody")),
            $"{Token("MessageBody")} Abre {Token("GuestAccessUrl")}.",
            ["AppName", "GuestAccessUrl", "MessageBody"]),
        new(
            CommunicationEmailTemplateKeys.PaymentInitialRequired,
            "Pagamento inicial pendente",
            "Pagamentos",
            "Email enviado quando o contrato entra em fase de pagamento inicial.",
            "1.0.0",
            "Default 2026",
            $"Pagamento inicial pendente — {Token("AppName")}",
            PaymentStatusEmailHtml(
                "Pagamento inicial",
                "O contrato passou para a fase de pagamento inicial.",
                "Consulta o detalhe no painel e acompanha a liquidação para o imóvel indicado.",
                "PaymentStatusLabel"),
            $"O {Token("PaymentTypeLabel")} do imóvel {Token("PropertyTitle")} está em estado {Token("PaymentStatusLabel")}. Montante: {Token("PaymentAmount")}. {Token("LeaseUrl")}",
            ["AppName", "PropertyTitle", "PaymentAmount", "PaymentTypeLabel", "PaymentStatusLabel", "LeaseUrl"]),
        new(
            CommunicationEmailTemplateKeys.PaymentSucceeded,
            "Pagamento confirmado",
            "Pagamentos",
            "Email enviado quando um pagamento é confirmado com sucesso.",
            "1.0.0",
            "Default 2026",
            $"Pagamento confirmado — {Token("AppName")}",
            PaymentStatusEmailHtml(
                "Pagamento confirmado",
                "Foi confirmada uma liquidação no fluxo do arrendamento.",
                "O pagamento encontra-se concluído e registado no histórico financeiro da plataforma.",
                "PaymentStatusLabel"),
            $"O {Token("PaymentTypeLabel")} do imóvel {Token("PropertyTitle")} foi confirmado no valor de {Token("PaymentAmount")}. {Token("LeaseUrl")}",
            ["AppName", "PropertyTitle", "PaymentAmount", "PaymentTypeLabel", "PaymentStatusLabel", "LeaseUrl"]),
        new(
            CommunicationEmailTemplateKeys.PaymentFailed,
            "Pagamento falhado",
            "Pagamentos",
            "Email enviado quando um pagamento não é concluído.",
            "1.0.0",
            "Default 2026",
            $"Ocorreu um problema com o pagamento — {Token("AppName")}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">O {Token("PaymentTypeLabel")} associado ao imóvel <strong>{Token("PropertyTitle")}</strong> não foi concluído.</p>
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 22px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                <tr>
                    <td style="padding:18px 20px;background-color:#fff7f5;border:1px solid #f1c9bc;border-radius:20px">
                        <p style="margin:0 0 8px;font-size:12px;line-height:1.4;letter-spacing:1.6px;text-transform:uppercase;font-weight:700;color:#b45309">Motivo</p>
                        <p style="margin:0;font-size:14px;line-height:1.7;color:#7c2d12">{Token("FailureReason")}</p>
                    </td>
                </tr>
            </table>
            <p style="margin:0 0 18px;font-size:14px;line-height:1.7;color:#475569">Valor esperado: <strong>{Token("PaymentAmount")}</strong>.</p>
            {ActionButton("LeaseUrl", "Rever pagamento")}
            {FallbackLink("LeaseUrl")}
            """,
            $"O {Token("PaymentTypeLabel")} do imóvel {Token("PropertyTitle")} falhou. Motivo: {Token("FailureReason")}. {Token("LeaseUrl")}",
            ["AppName", "PropertyTitle", "PaymentAmount", "PaymentTypeLabel", "FailureReason", "LeaseUrl"]),
        new(
            CommunicationEmailTemplateKeys.PaymentDepositRefunded,
            "Reembolso de caução",
            "Pagamentos",
            "Email enviado quando um reembolso de caução é processado.",
            "1.0.0",
            "Default 2026",
            $"Reembolso de caução processado — {Token("AppName")}",
            PaymentStatusEmailHtml(
                "Reembolso processado",
                "Foi registado um reembolso associado à caução do arrendamento.",
                "O movimento já consta do histórico financeiro do contrato.",
                "PaymentStatusLabel"),
            $"Foi processado um reembolso de caução para o imóvel {Token("PropertyTitle")} no valor de {Token("PaymentAmount")}. {Token("LeaseUrl")}",
            ["AppName", "PropertyTitle", "PaymentAmount", "PaymentTypeLabel", "PaymentStatusLabel", "LeaseUrl"]),
        new(
            CommunicationEmailTemplateKeys.LeaseNonRenewalNotice,
            "Não renovação do contrato",
            "Legal",
            "Comunicação legal de não renovação do contrato.",
            "1.0.0",
            "Default 2026",
            "Não Renovação de Contrato — Imóvel (Contrato {{LeaseReference}})",
            LeaseNoticeHtml(
                "Não renovação de contrato",
                "Informamos que o contrato de arrendamento não será renovado. O {{DecisionActor}} comunicou a sua decisão de não renovação.",
                "Nos termos do Art. 1081.º do Código Civil, o imóvel deve ser entregue nas condições previstas no contrato até à data de término. Esta comunicação foi registada para efeitos legais conforme o Art. 9.º do NRAU (Lei n.º 6/2006).",
                ("Data de término do contrato", "LeaseEndDate")),
            "O contrato não será renovado. {{DecisionActor}} decidiu não renovar. Data de término: {{LeaseEndDate}}. Referência: {{LeaseReference}}.",
            ["LeaseReference", "DecisionActor", "LeaseEndDate"]),
        new(
            CommunicationEmailTemplateKeys.LeaseEarlyTerminationNotice,
            "Denúncia antecipada",
            "Legal",
            "Comunicação legal relativa a denúncia antecipada do contrato.",
            "1.0.0",
            "Default 2026",
            "Denúncia Antecipada — Contrato {{LeaseReference}}",
            LeaseNoticeHtml(
                "Denúncia antecipada",
                "O inquilino comunicou a sua intenção de terminar antecipadamente o contrato.",
                "Esta comunicação tem valor legal nos termos do Art. 9.º do NRAU.",
                ("Data proposta", "ProposedTerminationDate"),
                ("Pré-aviso legal", "NoticeDays"),
                ("Indemnização", "IndemnificationAmount"),
                ("Motivo", "Reason")),
            "Pedido de denúncia antecipada para {{ProposedTerminationDate}}. Pré-aviso: {{NoticeDays}} dias. Indemnização: {{IndemnificationAmount}}. Motivo: {{Reason}}.",
            ["LeaseReference", "ProposedTerminationDate", "NoticeDays", "IndemnificationAmount", "Reason"]),
        new(
            CommunicationEmailTemplateKeys.LeaseRentIncreaseNotice,
            "Atualização de renda",
            "Legal",
            "Comunicação legal de atualização da renda do contrato.",
            "1.0.0",
            "Default 2026",
            "Atualização de Renda — Contrato {{LeaseReference}}",
            LeaseNoticeHtml(
                "Atualização de renda",
                "O senhorio comunicou uma atualização da renda do teu arrendamento.",
                "Tens até {{ContestationDeadline}} para contestar esta atualização. Esta comunicação tem valor legal nos termos do Art. 24.º do NRAU.",
                ("Renda atual", "CurrentRent"),
                ("Nova renda", "NewRent"),
                ("Coeficiente aplicado", "CoefficientApplied"),
                ("Data de entrada em vigor", "EffectiveDate")),
            "Atualização de renda: {{CurrentRent}} -> {{NewRent}}. Coeficiente {{CoefficientApplied}}. Entra em vigor em {{EffectiveDate}}. Contestação até {{ContestationDeadline}}.",
            ["LeaseReference", "CurrentRent", "NewRent", "IncreasePercentage", "CoefficientApplied", "EffectiveDate", "ContestationDeadline"]),
        new(
            CommunicationEmailTemplateKeys.LeaseRentIncreaseContestation,
            "Contestação de atualização de renda",
            "Legal",
            "Comunicação legal enviada ao senhorio quando a atualização é contestada.",
            "1.0.0",
            "Default 2026",
            "Contestação de Atualização de Renda — Contrato {{LeaseReference}}",
            LeaseNoticeHtml(
                "Contestação de atualização de renda",
                "O inquilino contestou a atualização de renda proposta.",
                "A atualização de renda fica suspensa até resolução da contestação.",
                ("Aumento contestado", "CurrentRent"),
                ("Nova renda proposta", "NewRent"),
                ("Motivo da contestação", "Reason")),
            "O inquilino contestou a atualização de renda. Atual: {{CurrentRent}}. Nova: {{NewRent}}. Motivo: {{Reason}}.",
            ["LeaseReference", "CurrentRent", "NewRent", "Reason"]),
        new(
            CommunicationEmailTemplateKeys.LegalDocumentUpdated,
            "Atualização de documento legal",
            "Legal",
            "Aviso enviado aos utilizadores quando um documento legal é publicado.",
            "1.0.0",
            "Default 2026",
            "Atualizámos {{DocumentTypeLabel}} — {{AppName}}",
            $"""
            <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">Publicámos uma nova versão de <strong>{Token("DocumentTitle")}</strong> na plataforma {Token("AppName")}.</p>
            {AccentPanel("O que mudou", "Atualizámos o documento legal e já tens a versão mais recente disponível para consulta no frontend web.")}
            <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 22px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                <tr>
                    <td style="padding:18px 20px;background-color:#f7f3ee;background-image:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);border:1px solid #e5d6c6;border-radius:20px">
                        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;mso-table-lspace:0pt;mso-table-rspace:0pt">
                            <tr>
                                <td style="padding:0 14px 10px 0;font-size:12px;line-height:1.5;font-weight:700;letter-spacing:1.2px;text-transform:uppercase;color:#1e6b66;vertical-align:top;white-space:nowrap">Documento</td>
                                <td style="padding:0 0 10px;font-size:14px;line-height:1.7;color:#334155">{Token("DocumentTitle")}</td>
                            </tr>
                            <tr>
                                <td style="padding:0 14px 0 0;font-size:12px;line-height:1.5;font-weight:700;letter-spacing:1.2px;text-transform:uppercase;color:#1e6b66;vertical-align:top;white-space:nowrap">Versão</td>
                                <td style="padding:0;font-size:14px;line-height:1.7;color:#334155">{Token("DocumentVersion")}</td>
                            </tr>
                        </table>
                    </td>
                </tr>
            </table>
            {ActionButton("DocumentUrl", "Consultar documento")}
            {FallbackLink("DocumentUrl")}
            <p style="margin:18px 0 0;font-size:13px;line-height:1.7;color:#64748b">Este aviso não detalha as alterações. O link abre a versão publicada no frontend web.</p>
            """,
            "Publicámos uma nova versão de {{DocumentTitle}} ({{DocumentVersion}}). Consulta em {{DocumentUrl}}.",
            ["AppName", "DocumentTitle", "DocumentTypeLabel", "DocumentVersion", "DocumentUrl"])
    ];

    public static readonly IReadOnlyList<LegalDocumentSeedDefinition> LegalDocuments =
    [
        new(
            "privacy_policy",
            "Política de Privacidade",
            "1.0.0",
            "Como tratamos dados pessoais, comunicações e segurança na plataforma.",
            "Versão inicial do documento de privacidade.",
            PrivacyPolicyHtml(),
            $"Política de Privacidade de {Token("AppName")} em vigor. Contacto: {Token("SupportEmail")}",
            ["AppName", "CompanyName", "CompanyAddress", "SupportEmail", "WebsiteUrl", "CurrentYear"]),
        new(
            "terms_of_use",
            "Termos de Utilização",
            "1.0.0",
            "Regras de utilização da plataforma para senhorios, inquilinos e convidados.",
            "Versão inicial dos termos de utilização.",
            TermsOfUseHtml(),
            $"Termos de Utilização de {Token("AppName")} em vigor. Contacto: {Token("SupportEmail")}",
            ["AppName", "CompanyName", "CompanyAddress", "SupportEmail", "WebsiteUrl", "CurrentYear"])
    ];

    public static EmailTemplateSeedDefinition GetEmailTemplateDefinition(string key)
        => EmailTemplates.First(template => string.Equals(template.Key, key, StringComparison.OrdinalIgnoreCase));

    public static LegalDocumentSeedDefinition GetLegalDocumentDefinition(string documentType)
        => LegalDocuments.First(document => string.Equals(document.DocumentType, documentType, StringComparison.OrdinalIgnoreCase));

    public static IReadOnlyList<CommunicationVariableDefinition> GetVariablesForTemplate(string? templateKey)
    {
        if (string.IsNullOrWhiteSpace(templateKey))
            return Variables;

        return Variables
            .Where(variable => variable.AppliesToKeys == null || variable.AppliesToKeys.Count == 0 || variable.AppliesToKeys.Contains(templateKey))
            .ToArray();
    }

    public static IReadOnlyList<CommunicationVariableDefinition> GetVariablesForDocument(string documentType)
        => LegalDocuments
            .Where(document => string.Equals(document.DocumentType, documentType, StringComparison.OrdinalIgnoreCase))
            .SelectMany(document => Variables.Where(variable => document.SupportedVariables.Contains(variable.Key)))
            .DistinctBy(variable => variable.Key)
            .ToArray();

    private static string PrivacyPolicyHtml()
        => $"""
        <section style="display:flex;flex-direction:column;gap:22px">
            <div>
                <p style="margin:0 0 10px;font-size:12px;line-height:1.4;letter-spacing:1.8px;text-transform:uppercase;font-weight:700;color:#1e6b66">Privacidade</p>
                <h2 style="margin:0 0 10px;font-size:28px;line-height:1.15;color:#0f172a">Como {Token("AppName")} trata os teus dados</h2>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Esta política descreve como {Token("CompanyName")} recolhe, utiliza, partilha e protege dados pessoais quando utilizas a plataforma, o website e os fluxos transacionais associados ao arrendamento.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">1. Responsável pelo tratamento</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">O responsável pelo tratamento é {Token("CompanyName")}, com sede em {Token("CompanyAddress")}. Para qualquer questão sobre privacidade, podes contactar-nos através de {Token("SupportEmail")}.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">2. Dados que recolhemos</h3>
                <p style="margin:0 0 10px;font-size:15px;line-height:1.8;color:#475569">Podemos tratar dados de identificação, contacto, autenticação, documentos submetidos, informação contratual, comunicações entre utilizadores, dados de pagamento e registos técnicos necessários para operação e segurança da plataforma.</p>
                <ul style="margin:0;padding-left:20px;color:#475569;font-size:15px;line-height:1.8">
                    <li>Dados de conta e autenticação</li>
                    <li>Dados de perfil e candidatura</li>
                    <li>Informação contratual e de pagamento</li>
                    <li>Logs técnicos, segurança e prevenção de fraude</li>
                </ul>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">3. Finalidades e bases legais</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Tratamos dados para executar o serviço, gerir contratos, disponibilizar suporte, cumprir obrigações legais, prevenir abuso e melhorar a plataforma. Sempre que aplicável, o tratamento baseia-se na execução do contrato, cumprimento de obrigações legais, interesse legítimo e, quando necessário, consentimento.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">4. Partilha de dados</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Podemos partilhar dados com fornecedores tecnológicos, parceiros de pagamento, serviços de notificações, autoridades competentes e outros intervenientes diretamente envolvidos no fluxo do arrendamento, sempre no estrito âmbito necessário.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">5. Conservação</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Conservamos os dados apenas durante o período necessário para as finalidades descritas, para defesa de direitos e para cumprimento de obrigações legais, fiscais, contabilísticas e de auditoria.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">6. Direitos do titular</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Podes solicitar acesso, retificação, apagamento, limitação, oposição e portabilidade, nos termos legalmente aplicáveis. Também podes apresentar reclamação à autoridade de controlo competente.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">7. Segurança</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Aplicamos medidas técnicas e organizativas adequadas para proteger confidencialidade, integridade, disponibilidade e rastreabilidade das operações críticas na plataforma.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">8. Contacto</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Para qualquer assunto relacionado com esta política, contacta {Token("SupportEmail")} ou consulta {Token("WebsiteUrl")}. Esta versão mantém-se em vigor até publicação de nova revisão.</p>
            </div>
        </section>
        """;

    private static string TermsOfUseHtml()
        => $"""
        <section style="display:flex;flex-direction:column;gap:22px">
            <div>
                <p style="margin:0 0 10px;font-size:12px;line-height:1.4;letter-spacing:1.8px;text-transform:uppercase;font-weight:700;color:#1e6b66">Termos</p>
                <h2 style="margin:0 0 10px;font-size:28px;line-height:1.15;color:#0f172a">Termos de Utilização da plataforma {Token("AppName")}</h2>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Os presentes termos regulam o acesso e utilização da plataforma disponibilizada por {Token("CompanyName")}, incluindo website, aplicações, fluxos de arrendamento, comunicações e serviços associados.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">1. Âmbito</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Ao utilizar a plataforma, aceitas cumprir estes termos, a política de privacidade e as regras específicas aplicáveis a determinadas funcionalidades, fluxos contratuais e serviços complementares.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">2. Conta e acesso</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">És responsável por manter a segurança das credenciais, pela veracidade da informação submetida e pela utilização diligente da tua conta. Podemos suspender acessos em caso de fraude, abuso, incumprimento legal ou risco operacional.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">3. Utilização permitida</h3>
                <p style="margin:0 0 10px;font-size:15px;line-height:1.8;color:#475569">A plataforma deve ser utilizada apenas para finalidades legítimas relacionadas com o arrendamento e serviços conexos.</p>
                <ul style="margin:0;padding-left:20px;color:#475569;font-size:15px;line-height:1.8">
                    <li>Não podes inserir informação falsa, enganosa ou ilegal</li>
                    <li>Não podes contornar mecanismos de segurança ou auditoria</li>
                    <li>Não podes usar a plataforma para spam, scraping indevido ou automação abusiva</li>
                </ul>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">4. Conteúdo e responsabilidade dos utilizadores</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Cada utilizador é responsável pelo conteúdo, documentos, comunicações e declarações que submete. {Token("CompanyName")} pode moderar, limitar ou remover conteúdo que viole estes termos, direitos de terceiros ou obrigações legais.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">5. Serviços, pagamentos e disponibilidade</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Podemos atualizar, alterar ou descontinuar funcionalidades quando necessário. A disponibilidade pode ser afetada por manutenção, integrações externas ou eventos fora do nosso controlo. Quando existirem pagamentos, aplicam-se também as condições apresentadas no fluxo transacional correspondente.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">6. Propriedade intelectual</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">A plataforma, design, marca, software, bases de dados e documentação pertencem a {Token("CompanyName")} ou aos respetivos licenciadores. Não é concedido qualquer direito de exploração além do estritamente necessário à utilização regular do serviço.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">7. Alterações</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Podemos rever estes termos a qualquer momento. A versão publicada no frontend web é a versão em vigor. Sempre que relevante, poderemos notificar os utilizadores através da plataforma ou por email.</p>
            </div>
            <div>
                <h3 style="margin:0 0 8px;font-size:18px;color:#0f172a">8. Contacto</h3>
                <p style="margin:0;font-size:15px;line-height:1.8;color:#475569">Para esclarecimentos sobre estes termos, contacta {Token("SupportEmail")} ou consulta {Token("WebsiteUrl")}. {Token("CompanyName")} mantém sede em {Token("CompanyAddress")}.</p>
            </div>
        </section>
        """;

    private static string StatusEmailHtml(string title, string message)
        => $"""
        <p style="margin:0 0 10px;font-size:12px;line-height:1.4;letter-spacing:1.8px;text-transform:uppercase;font-weight:700;color:#a65710">{title}</p>
        <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">{message}</p>
        {AccentPanel("Área segura", "Podes consultar o estado atualizado através da tua área segura de convidado.")}
        {ActionButton("GuestAccessUrl", "Consultar candidatura")}
        {FallbackLink("GuestAccessUrl")}
        """;

    private static string GuestLeaseEmailHtml(string title, string message)
        => $"""
        <p style="margin:0 0 10px;font-size:12px;line-height:1.4;letter-spacing:1.8px;text-transform:uppercase;font-weight:700;color:#a65710">{title}</p>
        <p style="margin:0 0 16px;font-size:15px;line-height:1.7;color:#334155">{message}</p>
        {AccentPanel("Área segura de fiador", "Abre a tua área segura para acompanhar o estado do contrato e concluir o próximo passo.")}
        {ActionButton("GuestAccessUrl", "Abrir área de fiador")}
        {FallbackLink("GuestAccessUrl")}
        """;

    private static string PaymentStatusEmailHtml(string title, string intro, string guidance, string statusVariableKey)
        => $"""
        <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">{intro}</p>
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 22px;mso-table-lspace:0pt;mso-table-rspace:0pt">
            <tr>
                <td style="padding:18px 20px;background-color:#f7f3ee;background-image:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);border:1px solid #e5d6c6;border-radius:20px">
                    <p style="margin:0 0 8px;font-size:12px;line-height:1.4;letter-spacing:1.6px;text-transform:uppercase;font-weight:700;color:#a65710">{title}</p>
                    <p style="margin:0 0 10px;font-size:16px;line-height:1.6;color:#0f172a;font-weight:700">{Token("PaymentTypeLabel")} · {Token(statusVariableKey)}</p>
                    <p style="margin:0 0 6px;font-size:14px;line-height:1.7;color:#475569">Imóvel: <strong>{Token("PropertyTitle")}</strong></p>
                    <p style="margin:0;font-size:14px;line-height:1.7;color:#475569">Montante: <strong>{Token("PaymentAmount")}</strong></p>
                </td>
            </tr>
        </table>
        <p style="margin:0 0 18px;font-size:14px;line-height:1.7;color:#475569">{guidance}</p>
        {ActionButton("LeaseUrl", "Abrir contrato")}
        {FallbackLink("LeaseUrl")}
        """;

    private static string LeaseNoticeHtml(string title, string intro, string legalNote, params (string Label, string VariableKey)[] rows)
    {
        var rowBuilder = new StringBuilder();
        foreach (var row in rows)
        {
            rowBuilder.Append($"""
                <tr>
                    <td style="padding:0 14px 10px 0;font-size:12px;line-height:1.5;font-weight:700;letter-spacing:1.2px;text-transform:uppercase;color:#1e6b66;vertical-align:top;white-space:nowrap">{row.Label}</td>
                    <td style="padding:0 0 10px;font-size:14px;line-height:1.7;color:#334155">{Token(row.VariableKey)}</td>
                </tr>
                """);
        }

        return $"""
               <p style="margin:0 0 16px;font-size:16px;line-height:1.7;color:#334155">{intro}</p>
               <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 18px;mso-table-lspace:0pt;mso-table-rspace:0pt">
                   <tr>
                       <td style="padding:18px 20px;background-color:#f7f3ee;background-image:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);border:1px solid #e5d6c6;border-radius:20px">
                           <p style="margin:0 0 12px;font-size:12px;line-height:1.4;letter-spacing:1.6px;text-transform:uppercase;font-weight:700;color:#a65710">{title}</p>
                           <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;mso-table-lspace:0pt;mso-table-rspace:0pt">
                               {rowBuilder}
                           </table>
                       </td>
                   </tr>
               </table>
               <p style="margin:0;font-size:13px;line-height:1.7;color:#64748b">{legalNote}</p>
               """;
    }

    private static string AccentPanel(string title, string message)
        => $"""
        <table role="presentation" width="100%" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 22px;mso-table-lspace:0pt;mso-table-rspace:0pt">
            <tr>
                <td style="padding:18px 20px;background-color:#f7f3ee;background-image:linear-gradient(90deg,#fff7ef 0%,#f3fbf9 100%);border:1px solid #e5d6c6;border-radius:20px">
                    <p style="margin:0 0 8px;font-size:12px;line-height:1.4;letter-spacing:1.6px;text-transform:uppercase;font-weight:700;color:#1e6b66">{title}</p>
                    <p style="margin:0;font-size:14px;line-height:1.7;color:#475569">{message}</p>
                </td>
            </tr>
        </table>
        """;

    private static string ActionButton(string urlVariableKey, string label)
        => $"""
        <table role="presentation" cellspacing="0" cellpadding="0" border="0" style="border-collapse:collapse;margin:0 0 20px;mso-table-lspace:0pt;mso-table-rspace:0pt">
            <tr>
                <td align="center" bgcolor="#1e6b66" style="border-radius:14px;background-color:#1e6b66;background-image:linear-gradient(135deg,#a65710 0%,#f2a04b 24%,#1e6b66 68%,#41b0a8 100%)">
                    <a href="{Token(urlVariableKey)}" style="display:inline-block;padding:13px 20px;color:#ffffff;text-decoration:none;font-weight:700;font-size:15px;line-height:1.2;border-radius:14px">{label}</a>
                </td>
            </tr>
        </table>
        """;

    private static string FallbackLink(string urlVariableKey)
        => $"""
        <p style="margin:0;font-size:12px;line-height:1.6;color:#64748b">Se o botão não funcionar, copia este endereço:<br /><span style="word-break:break-all;color:#334155">{Token(urlVariableKey)}</span></p>
        """;

    private static string Token(string key) => $"{{{{{key}}}}}";
}