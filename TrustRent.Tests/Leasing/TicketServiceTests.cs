using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Leasing.Contracts.DTOs;
using TrustRent.Modules.Leasing.Models;
using TrustRent.Modules.Leasing.Repositories;
using TrustRent.Modules.Leasing.Services;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Contracts.Models;

namespace TrustRent.Tests.Leasing;

public class TicketServiceTests
{
    private readonly Mock<ILeasingUnitOfWork> _uowMock;
    private readonly Mock<ITicketRepository> _ticketRepoMock;
    private readonly Mock<ILeaseAccessService> _leaseAccessMock;
    private readonly Mock<ICatalogAccessService> _catalogAccessMock;
    private readonly Mock<IUserService> _userServiceMock;
    private readonly Mock<INotificationService> _notificationMock;
    private readonly TicketService _sut;

    public TicketServiceTests()
    {
        _uowMock = new Mock<ILeasingUnitOfWork>();
        _ticketRepoMock = new Mock<ITicketRepository>();
        _leaseAccessMock = new Mock<ILeaseAccessService>();
        _catalogAccessMock = new Mock<ICatalogAccessService>();
        _userServiceMock = new Mock<IUserService>();
        _notificationMock = new Mock<INotificationService>();
        var loggerMock = new Mock<ILogger<TicketService>>();

        _uowMock.Setup(u => u.Tickets).Returns(_ticketRepoMock.Object);

        _sut = new TicketService(
            _uowMock.Object,
            _leaseAccessMock.Object,
            _catalogAccessMock.Object,
            _userServiceMock.Object,
            _notificationMock.Object,
            loggerMock.Object);
    }

    private LeaseAccessContext CreateLeaseContext(Guid? tenantId = null, Guid? landlordId = null) => new()
    {
        LeaseId = Guid.NewGuid(),
        TenantId = tenantId ?? Guid.NewGuid(),
        LandlordId = landlordId ?? Guid.NewGuid(),
        PropertyId = Guid.NewGuid(),
        MonthlyRent = 800m,
        LeaseStatus = "Active"
    };

    // --- CreateTicketAsync ---

    [Fact]
    public async Task CreateTicketAsync_ValidInput_CreatesTicket()
    {
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var leaseCtx = CreateLeaseContext(tenantId: tenantId);
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(leaseCtx);

        var dto = new CreateTicketDto { Title = "Water leak", Description = "The kitchen sink is leaking badly", Priority = "High" };
        var result = await _sut.CreateTicketAsync(leaseId, tenantId, dto);

        Assert.NotNull(result);
        Assert.Equal("Water leak", result.Title);
        _ticketRepoMock.Verify(r => r.AddAsync(It.IsAny<Ticket>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task CreateTicketAsync_LeaseNotFound_ThrowsKeyNotFoundException()
    {
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(It.IsAny<Guid>())).ReturnsAsync((LeaseAccessContext?)null);

        var dto = new CreateTicketDto { Title = "Test title", Description = "Test description long enough" };

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.CreateTicketAsync(Guid.NewGuid(), Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task CreateTicketAsync_NotTenant_ThrowsUnauthorizedAccessException()
    {
        var leaseId = Guid.NewGuid();
        var leaseCtx = CreateLeaseContext();
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(leaseCtx);

        var dto = new CreateTicketDto { Title = "Test title", Description = "Test description long enough" };

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.CreateTicketAsync(leaseId, Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task CreateTicketAsync_ShortTitle_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var leaseCtx = CreateLeaseContext(tenantId: tenantId);
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(leaseCtx);

        var dto = new CreateTicketDto { Title = "Ab", Description = "Valid description here" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateTicketAsync(leaseId, tenantId, dto));
    }

    [Fact]
    public async Task CreateTicketAsync_ShortDescription_ThrowsArgumentException()
    {
        var tenantId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var leaseCtx = CreateLeaseContext(tenantId: tenantId);
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(leaseCtx);

        var dto = new CreateTicketDto { Title = "Valid title", Description = "Short" };

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.CreateTicketAsync(leaseId, tenantId, dto));
    }

    [Fact]
    public async Task CreateTicketAsync_SendsNotificationToLandlord()
    {
        var tenantId = Guid.NewGuid();
        var landlordId = Guid.NewGuid();
        var leaseId = Guid.NewGuid();
        var leaseCtx = CreateLeaseContext(tenantId: tenantId, landlordId: landlordId);
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(leaseCtx);

        var dto = new CreateTicketDto { Title = "Broken window", Description = "The window in the bedroom is broken" };
        await _sut.CreateTicketAsync(leaseId, tenantId, dto);

        _notificationMock.Verify(n => n.SendNotificationAsync(
            landlordId, It.IsAny<string>(), It.IsAny<string>(), It.IsAny<Guid?>()), Times.Once);
    }

    // --- GetTicketsByLeaseAsync ---

    [Fact]
    public async Task GetTicketsByLeaseAsync_LeaseNotFound_ThrowsKeyNotFoundException()
    {
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(It.IsAny<Guid>())).ReturnsAsync((LeaseAccessContext?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.GetTicketsByLeaseAsync(Guid.NewGuid(), Guid.NewGuid()));
    }

    [Fact]
    public async Task GetTicketsByLeaseAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var leaseId = Guid.NewGuid();
        var leaseCtx = CreateLeaseContext();
        _leaseAccessMock.Setup(l => l.GetLeaseAccessContextAsync(leaseId)).ReturnsAsync(leaseCtx);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetTicketsByLeaseAsync(leaseId, Guid.NewGuid()));
    }

    // --- GetTicketByIdAsync ---

    [Fact]
    public async Task GetTicketByIdAsync_NonExistent_ReturnsNull()
    {
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(It.IsAny<Guid>())).ReturnsAsync((Ticket?)null);

        var result = await _sut.GetTicketByIdAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetTicketByIdAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = "Test",
            Description = "Test"
        };
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(ticket.Id)).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.GetTicketByIdAsync(ticket.Id, Guid.NewGuid()));
    }

    // --- UpdateTicketStatusAsync ---

    [Fact]
    public async Task UpdateTicketStatusAsync_TicketNotFound_ThrowsKeyNotFoundException()
    {
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(It.IsAny<Guid>())).ReturnsAsync((Ticket?)null);

        await Assert.ThrowsAsync<KeyNotFoundException>(
            () => _sut.UpdateTicketStatusAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateTicketStatusDto { Status = "InProgress" }));
    }

    [Fact]
    public async Task UpdateTicketStatusAsync_InvalidTransition_ThrowsInvalidOperationException()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = "Test",
            Description = "Test",
            Status = TicketStatus.Closed // Closed can't transition to anything
        };
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(ticket.Id)).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.UpdateTicketStatusAsync(ticket.Id, ticket.LandlordId, new UpdateTicketStatusDto { Status = "InProgress" }));
    }

    [Fact]
    public async Task UpdateTicketStatusAsync_TenantTriesToSetInProgress_ThrowsUnauthorizedAccessException()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = "Test",
            Description = "Test",
            Status = TicketStatus.Open
        };
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(ticket.Id)).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.UpdateTicketStatusAsync(ticket.Id, ticket.TenantId, new UpdateTicketStatusDto { Status = "InProgress" }));
    }

    // --- AddCommentAsync ---

    [Fact]
    public async Task AddCommentAsync_ValidInput_AddsComment()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "Test Description"
        };
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(ticket.Id)).ReturnsAsync(ticket);

        var dto = new AddTicketCommentDto { Content = "This is my comment" };
        var result = await _sut.AddCommentAsync(ticket.Id, ticket.TenantId, dto);

        _ticketRepoMock.Verify(r => r.AddCommentAsync(It.IsAny<TicketComment>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddCommentAsync_ShortContent_ThrowsArgumentException()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = "Test",
            Description = "Test"
        };
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(ticket.Id)).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.AddCommentAsync(ticket.Id, ticket.TenantId, new AddTicketCommentDto { Content = "x" }));
    }

    // --- AddAttachmentAsync ---

    [Fact]
    public async Task AddAttachmentAsync_ValidInput_AddsAttachment()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = "Test Ticket",
            Description = "Test Description"
        };
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(ticket.Id)).ReturnsAsync(ticket);

        var result = await _sut.AddAttachmentAsync(ticket.Id, ticket.TenantId, "https://storage.example.com/file.jpg", "file.jpg");

        _ticketRepoMock.Verify(r => r.AddAttachmentAsync(It.IsAny<TicketAttachment>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task AddAttachmentAsync_UnauthorizedUser_ThrowsUnauthorizedAccessException()
    {
        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            LeaseId = Guid.NewGuid(),
            TenantId = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            Title = "Test",
            Description = "Test"
        };
        _ticketRepoMock.Setup(r => r.GetByIdWithCommentsAndAttachmentsAsync(ticket.Id)).ReturnsAsync(ticket);

        await Assert.ThrowsAsync<UnauthorizedAccessException>(
            () => _sut.AddAttachmentAsync(ticket.Id, Guid.NewGuid(), "https://url.com/file.jpg", "file.jpg"));
    }
}
