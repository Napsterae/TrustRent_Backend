using TrustRent.Modules.Leasing.Contracts.DTOs;

namespace TrustRent.Modules.Leasing.Contracts.Interfaces;

public interface IReviewService
{
    Task<ReviewResponse> SubmitReviewAsync(Guid userId, CreateReviewRequest request);
    Task<IEnumerable<ReviewResponse>> GetPublicReviewsForUserAsync(Guid userId);
    Task<UserReviewSummary> GetReviewSummaryForUserAsync(Guid userId);
    Task<IEnumerable<PendingReviewResponse>> GetPendingReviewsForUserAsync(Guid userId);
    Task<IEnumerable<ReviewResponse>> GetReviewsByLeaseAsync(Guid leaseId, Guid userId);
    Task<IEnumerable<ReviewResponse>> GetReviewsByTicketAsync(Guid ticketId, Guid userId);

    // Called by scheduler
    Task CreateLeaseReviewPairAsync(Guid leaseId);
    Task CreateTicketReviewPairAsync(Guid ticketId);
    Task ProcessExpiredReviewsAsync();
}
