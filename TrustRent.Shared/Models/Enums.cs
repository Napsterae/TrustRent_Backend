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
    GeneratingContract = 10
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
    GeneratingContract = 11
}
