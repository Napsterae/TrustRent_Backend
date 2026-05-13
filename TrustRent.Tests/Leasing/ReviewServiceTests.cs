using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Modules.Leasing.Contracts.Database;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Tests.Leasing;

public class ReviewServiceTests
{
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<INotificationService> _notificationMock;

    public ReviewServiceTests()
    {
        _userServiceMock = new Mock<IUserService>();
        _notificationMock = new Mock<INotificationService>();

        _userServiceMock.Setup(s => s.GetProfileAsync(It.IsAny<Guid>()))
            .ReturnsAsync((Guid userId) => new User
            {
                Id = userId,
                Name = $"User {userId:N}"[..12],
                Email = $"{userId:N}@example.com",
                TrustScore = 50
            });
        _userServiceMock.Setup(s => s.UpdateTrustScoreAsync(It.IsAny<Guid>(), It.IsAny<int>()))
            .Returns(Task.CompletedTask);
    }

    private (ReviewService Service, LeasingDbContext Context) CreateService()
    {
        var options = new DbContextOptionsBuilder<LeasingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var context = new LeasingDbContext(options);
        var logger = new Mock<ILogger<ReviewService>>();

        return (
            new ReviewService(context, _userServiceMock.Object, _notificationMock.Object, logger.Object),
            context);
    }

    [Fact]
    public async Task SubmitReviewAsync_WhenPairPublishes_NotifiesBothReviewedUsers()
    {
        var (service, context) = CreateService();
        var pairId = Guid.NewGuid();
        var reviewerId = Guid.NewGuid();
        var otherReviewerId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();

        context.Reviews.AddRange(
            new Review
            {
                Id = Guid.NewGuid(),
                PairId = pairId,
                ReviewerId = reviewerId,
                ReviewedUserId = otherReviewerId,
                LeaseId = leaseId,
                Type = ReviewType.LeaseReview,
                Status = ReviewStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddDays(5)
            },
            new Review
            {
                Id = Guid.NewGuid(),
                PairId = pairId,
                ReviewerId = otherReviewerId,
                ReviewedUserId = reviewerId,
                LeaseId = leaseId,
                Type = ReviewType.LeaseReview,
                Status = ReviewStatus.Submitted,
                SubmittedAt = DateTime.UtcNow.AddMinutes(-10),
                ExpiresAt = DateTime.UtcNow.AddDays(5)
            });
        await context.SaveChangesAsync();

        var response = await service.SubmitReviewAsync(reviewerId, new CreateReviewRequest
        {
            PairId = pairId,
            Rating = 5,
            Comment = "Tudo impecável"
        });

        Assert.Equal("Published", response.Status);
        _notificationMock.Verify(n => n.SendNotificationAsync(otherReviewerId, "review", It.Is<string>(s => s.Contains("arrendamento")), leaseId), Times.Once);
        _notificationMock.Verify(n => n.SendNotificationAsync(reviewerId, "review", It.Is<string>(s => s.Contains("arrendamento")), leaseId), Times.Once);

        context.Dispose();
    }

    [Fact]
    public async Task ProcessExpiredReviewsAsync_PublishesSubmittedReviewsAndNotifiesReviewedUser()
    {
        var (service, context) = CreateService();
        var pairId = Guid.NewGuid();
        var reviewedUserId = Guid.NewGuid();
        var ticketId = Guid.NewGuid();

        context.Reviews.AddRange(
            new Review
            {
                Id = Guid.NewGuid(),
                PairId = pairId,
                ReviewerId = Guid.NewGuid(),
                ReviewedUserId = reviewedUserId,
                TicketId = ticketId,
                Type = ReviewType.TicketReview,
                Status = ReviewStatus.Submitted,
                SubmittedAt = DateTime.UtcNow.AddDays(-1),
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            },
            new Review
            {
                Id = Guid.NewGuid(),
                PairId = pairId,
                ReviewerId = Guid.NewGuid(),
                ReviewedUserId = Guid.NewGuid(),
                TicketId = ticketId,
                Type = ReviewType.TicketReview,
                Status = ReviewStatus.Pending,
                ExpiresAt = DateTime.UtcNow.AddMinutes(-1)
            });
        await context.SaveChangesAsync();

        await service.ProcessExpiredReviewsAsync();

        var reviews = await context.Reviews.Where(r => r.PairId == pairId).OrderBy(r => r.Status).ToListAsync();
        Assert.Contains(reviews, r => r.ReviewedUserId == reviewedUserId && r.Status == ReviewStatus.Published);
        Assert.Contains(reviews, r => r.Status == ReviewStatus.Expired);
        _notificationMock.Verify(n => n.SendNotificationAsync(reviewedUserId, "review", It.Is<string>(s => s.Contains("ticket")), ticketId), Times.Once);

        context.Dispose();
    }
}