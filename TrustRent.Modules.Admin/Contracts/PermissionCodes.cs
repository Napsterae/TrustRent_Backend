namespace TrustRent.Modules.Admin.Contracts;

public static class PermissionCodes
{
    // Admins
    public const string AdminsRead = "admins.read";
    public const string AdminsCreate = "admins.create";
    public const string AdminsEdit = "admins.edit";
    public const string AdminsDelete = "admins.delete";
    public const string AdminsAssignPermissions = "admins.assign_permissions";
    public const string AdminsManageRoles = "admins.manage_roles";
    public const string AdminsManageSuperAdmin = "admins.manage_super_admin";
    public const string AdminsRevokeSessions = "admins.revoke_sessions";

    // Users
    public const string UsersRead = "users.read";
    public const string UsersSuspend = "users.suspend";
    public const string UsersUnsuspend = "users.unsuspend";
    public const string UsersAnonymize = "users.anonymize";
    public const string UsersEdit = "users.edit";
    public const string UsersAdjustTrustScore = "users.adjust_trustscore";
    public const string UsersForceReverify = "users.force_reverify";

    // Properties
    public const string PropertiesRead = "properties.read";
    public const string PropertiesApprove = "properties.approve";
    public const string PropertiesReject = "properties.reject";
    public const string PropertiesBlock = "properties.block";
    public const string PropertiesEdit = "properties.edit";
    public const string PropertiesDelete = "properties.delete";

    // Applications
    public const string ApplicationsRead = "applications.read";
    public const string ApplicationsChangeState = "applications.change_state";
    public const string ApplicationsCancel = "applications.cancel";
    public const string ApplicationsExport = "applications.export";
    public const string ApplicationsManageCoTenant = "applications.manage_cotenant";
    public const string ApplicationsManageGuarantor = "applications.manage_guarantor";
    public const string ApplicationsReviewGuarantor = "applications.review_guarantor";

    // Leases
    public const string LeasesRead = "leases.read";
    public const string LeasesTerminate = "leases.terminate";
    public const string LeasesEdit = "leases.edit";
    public const string LeasesUploadDocuments = "leases.upload_documents";
    public const string LeasesManageSignatures = "leases.manage_signatures";

    // Payments
    public const string PaymentsRead = "payments.read";
    public const string PaymentsRefund = "payments.refund";
    public const string PaymentsManualCharge = "payments.manual_charge";
    public const string PaymentsManualMarkPaid = "payments.manual_mark_paid";
    public const string PaymentsViewStripe = "payments.view_stripe";
    public const string PaymentsManageStripeAccounts = "payments.manage_stripe_accounts";

    // Tickets - maintenance
    public const string TicketsMaintenanceRead = "tickets.maintenance.read";
    public const string TicketsMaintenanceIntervene = "tickets.maintenance.intervene";
    public const string TicketsMaintenanceReassign = "tickets.maintenance.reassign";

    // Tickets - support
    public const string TicketsSupportRead = "tickets.support.read";
    public const string TicketsSupportRespond = "tickets.support.respond";
    public const string TicketsSupportAssign = "tickets.support.assign";
    public const string TicketsSupportClose = "tickets.support.close";

    // Reviews
    public const string ReviewsRead = "reviews.read";
    public const string ReviewsModerate = "reviews.moderate";
    public const string ReviewsDelete = "reviews.delete";

    // Communications
    public const string CommunicationsBroadcast = "communications.broadcast";
    public const string CommunicationsTemplatesEdit = "communications.templates.edit";
    public const string CommunicationsBannersEdit = "communications.banners.edit";

    // Reference data
    public const string ReferenceRead = "reference.read";
    public const string ReferenceAmenitiesEdit = "reference.amenities.edit";
    public const string ReferenceLocationsEdit = "reference.locations.edit";
    public const string ReferencePropertyOptionsEdit = "reference.property_options.edit";
    public const string ReferenceSalaryRangesEdit = "reference.salary_ranges.edit";
    public const string ReferencePhoneCountriesEdit = "reference.phone_countries.edit";

    // Settings
    public const string SettingsRead = "settings.read";
    public const string SettingsEdit = "settings.edit";
    public const string SettingsFeatureFlags = "settings.feature_flags";

    // Audit
    public const string AuditRead = "audit.read";
    public const string AuditExport = "audit.export";

    // Jobs
    public const string JobsRead = "jobs.read";
    public const string JobsRun = "jobs.run";

    public static IReadOnlyList<(string Code, string Description, string Category)> Catalog =
    [
        (AdminsRead, "Listar e ver administradores", "Administradores"),
        (AdminsCreate, "Criar novos administradores", "Administradores"),
        (AdminsEdit, "Editar administradores", "Administradores"),
        (AdminsDelete, "Eliminar administradores", "Administradores"),
        (AdminsAssignPermissions, "Atribuir permissões individuais", "Administradores"),
        (AdminsManageRoles, "Gerir roles administrativas", "Administradores"),
        (AdminsManageSuperAdmin, "Promover ou despromover super-admin", "Administradores"),
        (AdminsRevokeSessions, "Revogar sessões de administradores", "Administradores"),

        (UsersRead, "Listar e ver utilizadores públicos", "Utilizadores"),
        (UsersSuspend, "Suspender utilizadores", "Utilizadores"),
        (UsersUnsuspend, "Anular suspensão", "Utilizadores"),
        (UsersAnonymize, "Anonimizar utilizadores (RGPD)", "Utilizadores"),
        (UsersEdit, "Editar dados de utilizadores", "Utilizadores"),
        (UsersAdjustTrustScore, "Ajustar TrustScore manualmente", "Utilizadores"),
        (UsersForceReverify, "Forçar reverificação", "Utilizadores"),

        (PropertiesRead, "Listar e ver imóveis", "Imóveis"),
        (PropertiesApprove, "Aprovar publicação de imóveis", "Imóveis"),
        (PropertiesReject, "Rejeitar imóveis", "Imóveis"),
        (PropertiesBlock, "Bloquear/desbloquear imóveis", "Imóveis"),
        (PropertiesEdit, "Editar imóveis", "Imóveis"),
        (PropertiesDelete, "Eliminar imóveis", "Imóveis"),

        (ApplicationsRead, "Listar e ver candidaturas", "Candidaturas"),
        (ApplicationsChangeState, "Alterar estado manualmente", "Candidaturas"),
        (ApplicationsCancel, "Cancelar candidaturas", "Candidaturas"),
        (ApplicationsExport, "Exportar candidaturas", "Candidaturas"),
        (ApplicationsManageCoTenant, "Gerir co-candidatura", "Candidaturas"),
        (ApplicationsManageGuarantor, "Gerir requisito de fiador", "Candidaturas"),
        (ApplicationsReviewGuarantor, "Aprovar/rejeitar fiador", "Candidaturas"),

        (LeasesRead, "Listar e ver contratos", "Contratos"),
        (LeasesTerminate, "Terminar contratos antecipadamente", "Contratos"),
        (LeasesEdit, "Editar contratos", "Contratos"),
        (LeasesUploadDocuments, "Carregar documentos de contrato", "Contratos"),
        (LeasesManageSignatures, "Gerir assinaturas multi-parte", "Contratos"),

        (PaymentsRead, "Listar e ver pagamentos", "Pagamentos"),
        (PaymentsRefund, "Reembolsar pagamentos", "Pagamentos"),
        (PaymentsManualCharge, "Cobrança manual", "Pagamentos"),
        (PaymentsManualMarkPaid, "Marcar pagamento como pago", "Pagamentos"),
        (PaymentsViewStripe, "Ver dados Stripe", "Pagamentos"),
        (PaymentsManageStripeAccounts, "Gerir contas Stripe", "Pagamentos"),

        (TicketsMaintenanceRead, "Ler tickets de manutenção", "Tickets"),
        (TicketsMaintenanceIntervene, "Intervir em tickets de manutenção", "Tickets"),
        (TicketsMaintenanceReassign, "Reatribuir tickets de manutenção", "Tickets"),

        (TicketsSupportRead, "Ler tickets de suporte", "Tickets"),
        (TicketsSupportRespond, "Responder a tickets de suporte", "Tickets"),
        (TicketsSupportAssign, "Atribuir tickets de suporte", "Tickets"),
        (TicketsSupportClose, "Fechar tickets de suporte", "Tickets"),

        (ReviewsRead, "Ler reviews", "Reviews"),
        (ReviewsModerate, "Moderar reviews", "Reviews"),
        (ReviewsDelete, "Eliminar reviews", "Reviews"),

        (CommunicationsBroadcast, "Enviar comunicações em massa", "Comunicações"),
        (CommunicationsTemplatesEdit, "Editar templates", "Comunicações"),
        (CommunicationsBannersEdit, "Gerir banners", "Comunicações"),

        (ReferenceRead, "Ler dados de referência", "Reference Data"),
        (ReferenceAmenitiesEdit, "Editar comodidades", "Reference Data"),
        (ReferenceLocationsEdit, "Editar localizações", "Reference Data"),
        (ReferencePropertyOptionsEdit, "Editar opções de imóveis", "Reference Data"),
        (ReferenceSalaryRangesEdit, "Editar faixas salariais", "Reference Data"),
        (ReferencePhoneCountriesEdit, "Editar países telefónicos", "Reference Data"),

        (SettingsRead, "Ler configurações", "Configurações"),
        (SettingsEdit, "Editar configurações", "Configurações"),
        (SettingsFeatureFlags, "Gerir feature flags", "Configurações"),

        (AuditRead, "Ler auditoria", "Auditoria"),
        (AuditExport, "Exportar auditoria", "Auditoria"),

        (JobsRead, "Ler jobs", "Jobs"),
        (JobsRun, "Executar jobs manualmente", "Jobs"),
    ];
}
