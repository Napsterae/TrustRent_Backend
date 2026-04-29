namespace TrustRent.Shared.Models;

public enum LeaseStatus
{
    Pending,            // 0
    AwaitingSignatures, // 1
    PendingLandlordSignature, // 2
    PendingTenantSignature,   // 3
    SignaturesVerified, // 4
    AwaitingPayment,    // 5
    Active,             // 6
    Expired,            // 7
    TerminatedEarly,    // 8
    Cancelled,          // 9
    GeneratingContract = 10,
    PendingCoTenantSignature = 11,
    PendingGuarantorSignature = 12
}

public enum ApplicationStatus
{
    Pending,                  // 0
    VisitCounterProposed,     // 1
    VisitAccepted,            // 2
    InterestConfirmed,        // 3
    Accepted,                 // 4
    Rejected,                 // 5
    LeaseStartDateProposed,   // 6
    LeaseStartDateConfirmed,  // 7
    ContractPendingSignature, // 8
    AwaitingPayment,          // 9
    LeaseActive,              // 10
    GeneratingContract = 11,
    IncomeValidationRequested = 12, // entre InterestConfirmed e Accepted, opcional
    GuarantorRequested = 14,
    GuarantorReview = 15,
    GuarantorRejected = 16
}

// ===== Candidatura Conjunta & Fiador =====

public enum CoTenantInviteStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3,
    Expired = 4
}

public enum GuarantorInviteStatus
{
    Pending = 0,
    Accepted = 1,
    Declined = 2,
    Cancelled = 3,
    Expired = 4
}

public enum GuarantorRequirementStatus
{
    NotRequested = 0,
    Requested = 1,
    Submitted = 2,
    LandlordReviewing = 3,
    Approved = 4,
    Rejected = 5,
    Waived = 6
}

public enum LeaseSignatoryRole
{
    Landlord = 0,
    Tenant = 1,
    CoTenant = 2,
    Guarantor = 3
}

/// <summary>
/// Tipo de relação laboral declarado pelo inquilino na validação de rendimentos.
/// </summary>
public enum EmploymentType
{
    Employee = 0,      // Trabalhador por conta de outrem (CLT)
    SelfEmployed = 1   // Trabalhador independente / recibos verdes
}

/// <summary>
/// Combinação de provas usada para validar rendimentos numa candidatura.
/// </summary>
public enum IncomeValidationMethod
{
    Payslips = 0,                          // 3 recibos de vencimento (caso ideal)
    PayslipsWithEmployerDeclaration = 1,   // 1-2 recibos + declaração da entidade empregadora
    ActivityWithGreenReceipts = 2          // Declaração de início de atividade (Finanças) + recibos verdes
}
