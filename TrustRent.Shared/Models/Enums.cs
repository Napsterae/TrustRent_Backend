namespace TrustRent.Shared.Models;

public enum LeaseStatus
{
    Pending,
    AwaitingSignatures,
    PendingLandlordSignature,
    PendingTenantSignature,
    SignaturesVerified,
    AwaitingPayment,
    Active,
    Expired,
    TerminatedEarly,
    Cancelled
}

public enum ApplicationStatus
{
    Pending,
    VisitCounterProposed,
    VisitAccepted,
    InterestConfirmed,
    Accepted,
    Rejected,
    LeaseStartDateProposed,
    LeaseStartDateConfirmed,
    ContractPendingSignature,
    AwaitingPayment,
    LeaseActive
}
