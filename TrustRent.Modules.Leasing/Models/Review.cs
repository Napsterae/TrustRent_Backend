namespace TrustRent.Modules.Leasing.Models;

public class Review
{
    public Guid Id { get; set; }
    public Guid ReviewerId { get; set; }
    public Guid ReviewedUserId { get; set; }

    public Guid? LeaseId { get; set; }
    public Guid? TicketId { get; set; }

    public int Rating { get; set; }
    public string? Comment { get; set; }

    public ReviewType Type { get; set; }
    public ReviewStatus Status { get; set; } = ReviewStatus.Pending;

    /// <summary>
    /// Links the two cross-reviews together (landlord↔tenant).
    /// Both reviews in a pair share the same PairId.
    /// </summary>
    public Guid PairId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? PublishedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}

public enum ReviewType
{
    LeaseReview,
    TicketReview
}

public enum ReviewStatus
{
    Pending,
    Submitted,
    Published,
    Expired
}
