namespace TrustRent.Modules.Leasing.Contracts.DTOs;

public class CreateReviewRequest
{
    public Guid PairId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
}

public class ReviewResponse
{
    public Guid Id { get; set; }
    public Guid ReviewerId { get; set; }
    public string ReviewerName { get; set; } = string.Empty;
    public string? ReviewerProfilePictureUrl { get; set; }
    public Guid ReviewedUserId { get; set; }
    public string ReviewedUserName { get; set; } = string.Empty;
    public Guid? LeaseId { get; set; }
    public Guid? TicketId { get; set; }
    public int Rating { get; set; }
    public string? Comment { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
}

public class UserReviewSummary
{
    public Guid UserId { get; set; }
    public double AverageRating { get; set; }
    public int TotalReviews { get; set; }
    public int[] StarBreakdown { get; set; } = new int[5]; // index 0 = 1 star, index 4 = 5 stars
    public double? AverageLeaseRating { get; set; }
    public double? AverageTicketRating { get; set; }
    public int LeaseReviewCount { get; set; }
    public int TicketReviewCount { get; set; }
}

public class PendingReviewResponse
{
    public Guid PairId { get; set; }
    public Guid? LeaseId { get; set; }
    public Guid? TicketId { get; set; }
    public Guid ReviewedUserId { get; set; }
    public string ReviewedUserName { get; set; } = string.Empty;
    public string? ReviewedUserProfilePictureUrl { get; set; }
    public string Type { get; set; } = string.Empty;
    public DateTime ExpiresAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class RenewalResponseDto
{
    public string Response { get; set; } = string.Empty; // "Renew" or "Cancel"
}

public class LeaseRenewalStatusDto
{
    public Guid Id { get; set; }
    public Guid LeaseId { get; set; }
    public Guid LandlordId { get; set; }
    public Guid TenantId { get; set; }
    public DateTime NotifiedAt { get; set; }
    public DateTime DeadlineDate { get; set; }
    public string? LandlordResponse { get; set; }
    public DateTime? LandlordRespondedAt { get; set; }
    public string? TenantResponse { get; set; }
    public DateTime? TenantRespondedAt { get; set; }
    public bool Processed { get; set; }
    public int LandlordNoticeDays { get; set; }
    public int TenantNoticeDays { get; set; }
}
