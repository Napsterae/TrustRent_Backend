using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models;

namespace TrustRent.Modules.Leasing.Services;

public class ReviewService : IReviewService
{
    private readonly LeasingDbContext _db;
    private readonly IUserService _userService;
    private readonly INotificationService _notificationService;
    private readonly ILogger<ReviewService> _logger;

    public ReviewService(
        LeasingDbContext db,
        IUserService userService,
        INotificationService notificationService,
        ILogger<ReviewService> logger)
    {
        _db = db;
        _userService = userService;
        _notificationService = notificationService;
        _logger = logger;
    }

    public async Task<ReviewResponse> SubmitReviewAsync(Guid userId, CreateReviewRequest request)
    {
        if (request.Rating < 1 || request.Rating > 5)
            throw new ArgumentException("A classificação deve ser entre 1 e 5.");

        // Find the pending review for this user in this pair
        var review = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PairId == request.PairId && r.ReviewerId == userId && r.Status == ReviewStatus.Pending);

        if (review == null)
            throw new KeyNotFoundException("Review pendente não encontrada.");

        if (review.ExpiresAt < DateTime.UtcNow)
            throw new InvalidOperationException("O prazo para submeter esta review expirou.");

        review.Rating = request.Rating;
        review.Comment = request.Comment;
        review.Status = ReviewStatus.Submitted;
        review.SubmittedAt = DateTime.UtcNow;

        // Check if the paired review is also submitted
        var pairedReview = await _db.Reviews
            .FirstOrDefaultAsync(r => r.PairId == request.PairId && r.Id != review.Id);

        if (pairedReview?.Status == ReviewStatus.Submitted)
        {
            // Both submitted — publish both
            review.Status = ReviewStatus.Published;
            review.PublishedAt = DateTime.UtcNow;
            pairedReview.Status = ReviewStatus.Published;
            pairedReview.PublishedAt = DateTime.UtcNow;

            _logger.LogInformation("Review pair {PairId} published — both reviews submitted", request.PairId);
        }

        await _db.SaveChangesAsync();

        // Recalculate trust scores if published
        if (review.Status == ReviewStatus.Published)
        {
            await RecalculateTrustScoreAsync(review.ReviewedUserId);
            if (pairedReview != null)
                await RecalculateTrustScoreAsync(pairedReview.ReviewedUserId);
        }

        return await MapToResponseAsync(review);
    }

    public async Task<IEnumerable<ReviewResponse>> GetPublicReviewsForUserAsync(Guid userId)
    {
        var reviews = await _db.Reviews
            .Where(r => r.ReviewedUserId == userId && r.Status == ReviewStatus.Published)
            .OrderByDescending(r => r.PublishedAt)
            .ToListAsync();

        var responses = new List<ReviewResponse>();
        foreach (var r in reviews)
            responses.Add(await MapToResponseAsync(r));

        return responses;
    }

    public async Task<UserReviewSummary> GetReviewSummaryForUserAsync(Guid userId)
    {
        var reviews = await _db.Reviews
            .Where(r => r.ReviewedUserId == userId && r.Status == ReviewStatus.Published)
            .ToListAsync();

        var summary = new UserReviewSummary
        {
            UserId = userId,
            TotalReviews = reviews.Count,
            AverageRating = reviews.Count > 0 ? reviews.Average(r => r.Rating) : 0,
            StarBreakdown = new int[5]
        };

        foreach (var r in reviews)
            summary.StarBreakdown[r.Rating - 1]++;

        var leaseReviews = reviews.Where(r => r.Type == ReviewType.LeaseReview).ToList();
        var ticketReviews = reviews.Where(r => r.Type == ReviewType.TicketReview).ToList();

        summary.LeaseReviewCount = leaseReviews.Count;
        summary.TicketReviewCount = ticketReviews.Count;
        summary.AverageLeaseRating = leaseReviews.Count > 0 ? leaseReviews.Average(r => r.Rating) : null;
        summary.AverageTicketRating = ticketReviews.Count > 0 ? ticketReviews.Average(r => r.Rating) : null;

        return summary;
    }

    public async Task<IEnumerable<PendingReviewResponse>> GetPendingReviewsForUserAsync(Guid userId)
    {
        var reviews = await _db.Reviews
            .Where(r => r.ReviewerId == userId && r.Status == ReviewStatus.Pending && r.ExpiresAt > DateTime.UtcNow)
            .OrderBy(r => r.ExpiresAt)
            .ToListAsync();

        var responses = new List<PendingReviewResponse>();
        foreach (var r in reviews)
        {
            var reviewedUser = await _userService.GetProfileAsync(r.ReviewedUserId);
            responses.Add(new PendingReviewResponse
            {
                PairId = r.PairId,
                LeaseId = r.LeaseId,
                TicketId = r.TicketId,
                ReviewedUserId = r.ReviewedUserId,
                ReviewedUserName = reviewedUser?.Name ?? "Utilizador",
                ReviewedUserProfilePictureUrl = reviewedUser?.ProfilePictureUrl,
                Type = r.Type.ToString(),
                ExpiresAt = r.ExpiresAt,
                CreatedAt = r.CreatedAt
            });
        }

        return responses;
    }

    public async Task<IEnumerable<ReviewResponse>> GetReviewsByLeaseAsync(Guid leaseId, Guid userId)
    {
        var reviews = await _db.Reviews
            .Where(r => r.LeaseId == leaseId && r.Status == ReviewStatus.Published)
            .OrderByDescending(r => r.PublishedAt)
            .ToListAsync();

        var responses = new List<ReviewResponse>();
        foreach (var r in reviews)
            responses.Add(await MapToResponseAsync(r));

        return responses;
    }

    public async Task<IEnumerable<ReviewResponse>> GetReviewsByTicketAsync(Guid ticketId, Guid userId)
    {
        var reviews = await _db.Reviews
            .Where(r => r.TicketId == ticketId && r.Status == ReviewStatus.Published)
            .OrderByDescending(r => r.PublishedAt)
            .ToListAsync();

        var responses = new List<ReviewResponse>();
        foreach (var r in reviews)
            responses.Add(await MapToResponseAsync(r));

        return responses;
    }

    public async Task CreateLeaseReviewPairAsync(Guid leaseId)
    {
        var lease = await _db.Leases.FindAsync(leaseId)
            ?? throw new KeyNotFoundException("Arrendamento não encontrado.");

        var pairId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(10);

        // Check if there's already a pending/submitted review pair for this lease in the current period
        var existingRecent = await _db.Reviews
            .AnyAsync(r => r.LeaseId == leaseId && r.Type == ReviewType.LeaseReview
                && r.CreatedAt > DateTime.UtcNow.AddDays(-80) // ~3 months minus buffer
                && (r.Status == ReviewStatus.Pending || r.Status == ReviewStatus.Submitted));

        if (existingRecent)
        {
            _logger.LogInformation("Skipping lease review creation for {LeaseId} — recent pair exists", leaseId);
            return;
        }

        var landlordReview = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = lease.LandlordId,
            ReviewedUserId = lease.TenantId,
            LeaseId = leaseId,
            Type = ReviewType.LeaseReview,
            PairId = pairId,
            ExpiresAt = expiresAt
        };

        var tenantReview = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = lease.TenantId,
            ReviewedUserId = lease.LandlordId,
            LeaseId = leaseId,
            Type = ReviewType.LeaseReview,
            PairId = pairId,
            ExpiresAt = expiresAt
        };

        _db.Reviews.AddRange(landlordReview, tenantReview);
        await _db.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            lease.LandlordId, "ReviewRequest",
            "É hora de avaliar o seu inquilino! Tem 10 dias para submeter a sua avaliação.",
            leaseId);

        await _notificationService.SendNotificationAsync(
            lease.TenantId, "ReviewRequest",
            "É hora de avaliar o seu senhorio! Tem 10 dias para submeter a sua avaliação.",
            leaseId);

        _logger.LogInformation("Created lease review pair {PairId} for lease {LeaseId}", pairId, leaseId);
    }

    public async Task CreateTicketReviewPairAsync(Guid ticketId)
    {
        var ticket = await _db.Tickets.FindAsync(ticketId)
            ?? throw new KeyNotFoundException("Ticket não encontrado.");

        // Check if reviews already exist for this ticket
        var existingReviews = await _db.Reviews.AnyAsync(r => r.TicketId == ticketId);
        if (existingReviews)
        {
            _logger.LogInformation("Skipping ticket review creation for {TicketId} — already exists", ticketId);
            return;
        }

        var pairId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddDays(10);

        var landlordReview = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = ticket.LandlordId,
            ReviewedUserId = ticket.TenantId,
            TicketId = ticketId,
            Type = ReviewType.TicketReview,
            PairId = pairId,
            ExpiresAt = expiresAt
        };

        var tenantReview = new Review
        {
            Id = Guid.NewGuid(),
            ReviewerId = ticket.TenantId,
            ReviewedUserId = ticket.LandlordId,
            TicketId = ticketId,
            Type = ReviewType.TicketReview,
            PairId = pairId,
            ExpiresAt = expiresAt
        };

        _db.Reviews.AddRange(landlordReview, tenantReview);
        await _db.SaveChangesAsync();

        await _notificationService.SendNotificationAsync(
            ticket.LandlordId, "ReviewRequest",
            $"O ticket \"{ticket.Title}\" foi encerrado. Avalie o processo! Tem 10 dias.",
            ticketId);

        await _notificationService.SendNotificationAsync(
            ticket.TenantId, "ReviewRequest",
            $"O ticket \"{ticket.Title}\" foi encerrado. Avalie o processo! Tem 10 dias.",
            ticketId);

        _logger.LogInformation("Created ticket review pair {PairId} for ticket {TicketId}", pairId, ticketId);
    }

    public async Task ProcessExpiredReviewsAsync()
    {
        // Find all pairs where at least one review has expired
        var expiredPairIds = await _db.Reviews
            .Where(r => (r.Status == ReviewStatus.Pending || r.Status == ReviewStatus.Submitted)
                && r.ExpiresAt <= DateTime.UtcNow)
            .Select(r => r.PairId)
            .Distinct()
            .ToListAsync();

        foreach (var pairId in expiredPairIds)
        {
            var pair = await _db.Reviews
                .Where(r => r.PairId == pairId)
                .ToListAsync();

            foreach (var review in pair)
            {
                if (review.Status == ReviewStatus.Submitted)
                {
                    review.Status = ReviewStatus.Published;
                    review.PublishedAt = DateTime.UtcNow;
                }
                else if (review.Status == ReviewStatus.Pending)
                {
                    review.Status = ReviewStatus.Expired;
                }
            }

            _logger.LogInformation("Processed expired review pair {PairId}", pairId);
        }

        await _db.SaveChangesAsync();

        // Recalculate trust scores for users with newly published reviews
        var publishedUserIds = await _db.Reviews
            .Where(r => r.Status == ReviewStatus.Published && r.PublishedAt != null
                && r.PublishedAt > DateTime.UtcNow.AddMinutes(-5))
            .Select(r => r.ReviewedUserId)
            .Distinct()
            .ToListAsync();

        foreach (var userId in publishedUserIds)
        {
            await RecalculateTrustScoreAsync(userId);
        }
    }

    private async Task RecalculateTrustScoreAsync(Guid userId)
    {
        var user = await _userService.GetProfileAsync(userId);
        if (user == null) return;

        // Base score
        int score = 50;

        // Verification bonuses
        if (user.IsIdentityVerified) score += 20;
        if (user.IsNoDebtVerified) score += 15;

        // Review-based adjustment: up to +/- 15 points
        var reviews = await _db.Reviews
            .Where(r => r.ReviewedUserId == userId && r.Status == ReviewStatus.Published)
            .ToListAsync();

        if (reviews.Count > 0)
        {
            var avg = reviews.Average(r => r.Rating);
            // 3.0 = neutral (0 points), 5.0 = +15, 1.0 = -15
            var reviewBonus = (int)Math.Round((avg - 3.0) * 7.5);
            score += Math.Clamp(reviewBonus, -15, 15);
        }

        user.TrustScore = Math.Clamp(score, 0, 100);
        await _userService.UpdateTrustScoreAsync(userId, user.TrustScore);
        _logger.LogInformation("Recalculated TrustScore for user {UserId}: {Score}", userId, user.TrustScore);
    }

    private async Task<ReviewResponse> MapToResponseAsync(Review review)
    {
        var reviewer = await _userService.GetProfileAsync(review.ReviewerId);
        var reviewed = await _userService.GetProfileAsync(review.ReviewedUserId);

        return new ReviewResponse
        {
            Id = review.Id,
            ReviewerId = review.ReviewerId,
            ReviewerName = reviewer?.Name ?? "Utilizador",
            ReviewerProfilePictureUrl = reviewer?.ProfilePictureUrl,
            ReviewedUserId = review.ReviewedUserId,
            ReviewedUserName = reviewed?.Name ?? "Utilizador",
            LeaseId = review.LeaseId,
            TicketId = review.TicketId,
            Rating = review.Rating,
            Comment = review.Comment,
            Type = review.Type.ToString(),
            Status = review.Status.ToString(),
            CreatedAt = review.CreatedAt,
            PublishedAt = review.PublishedAt
        };
    }
}
