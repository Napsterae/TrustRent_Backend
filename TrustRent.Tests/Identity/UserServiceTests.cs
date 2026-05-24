using Moq;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Modules.Identity.Services;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Tests.Identity;

public class UserServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly Mock<IImageService> _imageServiceMock;
    private readonly Mock<IGeminiDocumentService> _geminiMock;
    private readonly Mock<IUserContactAccessService> _contactAccessMock;
    private readonly Mock<IWhatsAppCodeService> _whatsAppCodeServiceMock;
    private readonly Mock<ITelegramMessagingPlatformService> _telegramMessagingMock;
    private readonly UserService _sut;

    public UserServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _imageServiceMock = new Mock<IImageService>();
        _geminiMock = new Mock<IGeminiDocumentService>();
        _contactAccessMock = new Mock<IUserContactAccessService>();
        _whatsAppCodeServiceMock = new Mock<IWhatsAppCodeService>();
        _telegramMessagingMock = new Mock<ITelegramMessagingPlatformService>();

        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        _sut = new UserService(
            _uowMock.Object,
            _imageServiceMock.Object,
            _geminiMock.Object,
            _contactAccessMock.Object);
    }

    private User CreateTestUser(Guid? id = null) => new()
    {
        Id = id ?? Guid.NewGuid(),
        Name = "Test User",
        Email = "test@example.com",
        PasswordHash = BCrypt.Net.BCrypt.HashPassword("OldPassword"),
        TrustScore = 50,
        IsIdentityVerified = false,
        IsNoDebtVerified = false
    };

    // --- GetProfileAsync ---

    [Fact]
    public async Task GetProfileAsync_ExistingUser_ReturnsUser()
    {
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var result = await _sut.GetProfileAsync(userId);

        Assert.NotNull(result);
        Assert.Equal(userId, result!.Id);
    }

    [Fact]
    public async Task GetProfileAsync_NonExistingUser_ReturnsNull()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var result = await _sut.GetProfileAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // --- GetProfileDtoAsync ---

    [Fact]
    public async Task GetProfileDtoAsync_ExistingUser_ReturnsDto()
    {
        var user = CreateTestUser();
        user.Nif = "123456789";
        user.PhoneCountryCode = "PT";
        user.PhoneNumber = "+351912345678";
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var dto = await _sut.GetProfileDtoAsync(user.Id);

        Assert.NotNull(dto);
        Assert.Equal(user.Name, dto!.Name);
        Assert.Equal(user.Email, dto.Email);
        Assert.Equal(user.Nif, dto.Nif);
        Assert.Equal(user.TrustScore, dto.TrustScore);
    }

    [Fact]
    public async Task GetProfileDtoAsync_InvalidStoredPhonePlatform_FallsBackToTelegram()
    {
        var user = CreateTestUser();
        user.PhoneContactPlatform = "sms";
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var dto = await _sut.GetProfileDtoAsync(user.Id);

        Assert.NotNull(dto);
        Assert.Equal(PhoneContactPlatforms.Telegram, dto!.PhoneContactPlatform);
    }

    [Fact]
    public async Task GetProfileDtoAsync_NonExistingUser_ReturnsNull()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var result = await _sut.GetProfileDtoAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // --- GetPublicProfileAsync ---

    [Fact]
    public async Task GetPublicProfileAsync_SameUser_ShowsContactInfo()
    {
        var userId = Guid.NewGuid();
        var user = CreateTestUser(userId);
        user.Email = "visible@example.com";
        user.PhoneCountryCode = "PT";
        user.PhoneNumber = "+351912345678";
        _userRepoMock.Setup(r => r.GetByIdAsync(userId)).ReturnsAsync(user);

        var dto = await _sut.GetPublicProfileAsync(userId, userId);

        Assert.NotNull(dto);
        Assert.Equal("visible@example.com", dto!.Email);
        Assert.Equal("PT", dto.PhoneCountryCode);
        Assert.Equal("+351912345678", dto.PhoneNumber);
    }

    [Fact]
    public async Task GetPublicProfileAsync_DifferentUser_NoAccess_HidesContactInfo()
    {
        var targetId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var user = CreateTestUser(targetId);
        user.Email = "hidden@example.com";
        user.PhoneCountryCode = "PT";
        user.PhoneNumber = "+351912345678";
        _userRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(user);
        _contactAccessMock.Setup(s => s.CanViewDirectContactAsync(viewerId, targetId)).ReturnsAsync(false);

        var dto = await _sut.GetPublicProfileAsync(targetId, viewerId);

        Assert.NotNull(dto);
        Assert.Null(dto!.Email);
        Assert.Null(dto.PhoneCountryCode);
        Assert.Null(dto.PhoneNumber);
    }

    [Fact]
    public async Task GetPublicProfileAsync_DifferentUser_WithAccess_ShowsContactInfo()
    {
        var targetId = Guid.NewGuid();
        var viewerId = Guid.NewGuid();
        var user = CreateTestUser(targetId);
        user.Email = "visible@example.com";
        _userRepoMock.Setup(r => r.GetByIdAsync(targetId)).ReturnsAsync(user);
        _contactAccessMock.Setup(s => s.CanViewDirectContactAsync(viewerId, targetId)).ReturnsAsync(true);

        var dto = await _sut.GetPublicProfileAsync(targetId, viewerId);

        Assert.Equal("visible@example.com", dto!.Email);
    }

    // --- UpdateProfileAsync ---

    [Fact]
    public async Task UpdateProfileAsync_ValidData_UpdatesUser()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.IsNifUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.IsCcUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.IsEmailUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);

        var dto = new UpdateProfileDto("New Name", "new@example.com", "123456789", "12345678", "Rua X", "1000-001", null, null);

        await _sut.UpdateProfileAsync(user.Id, dto);

        Assert.Equal("New Name", user.Name);
        Assert.Equal("new@example.com", user.Email);
        Assert.Null(user.PhoneNumber);
        Assert.Null(user.PendingPhoneNumber);
        Assert.Null(user.PendingPhoneCountryCode);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_UnverifiedPhoneDraft_DoesNotPersistPendingPhone()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.IsNifUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.IsCcUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.IsEmailUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);

        var dto = new UpdateProfileDto(
            "New Name",
            "new@example.com",
            null,
            null,
            null,
            null,
            "PT",
            "+351912345678",
            PhoneContactPlatforms.Telegram);

        await _sut.UpdateProfileAsync(user.Id, dto);

        Assert.Null(user.PhoneNumber);
        Assert.Null(user.PhoneCountryCode);
        Assert.Null(user.PendingPhoneNumber);
        Assert.Null(user.PendingPhoneCountryCode);
        Assert.Null(user.PendingPhoneContactPlatform);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateProfileAsync_VerifiedPhone_ThrowsWhenChangingValidatedPhone()
    {
        var user = CreateTestUser();
        user.PhoneCountryCode = "PT";
        user.PhoneNumber = "+351911111111";
        user.PhoneContactPlatform = PhoneContactPlatforms.Telegram;
        user.IsPhoneNumberVerified = true;

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.IsEmailUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);

        var dto = new UpdateProfileDto(
            user.Name,
            user.Email,
            user.Nif,
            user.CitizenCardNumber,
            user.Address,
            user.PostalCode,
            "PT",
            "+351922222222",
            PhoneContactPlatforms.Telegram);

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));

        Assert.Contains("telemóvel", ex.Message);
        Assert.Equal("+351911111111", user.PhoneNumber);
        Assert.Equal("PT", user.PhoneCountryCode);
        Assert.True(user.IsPhoneNumberVerified);
        Assert.Null(user.PendingPhoneNumber);
        Assert.Null(user.PendingPhoneCountryCode);
        Assert.Null(user.PendingPhoneContactPlatform);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task UpdateProfileAsync_UserNotFound_ThrowsException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        var dto = new UpdateProfileDto("Name", "email@test.com", null, null, null, null, null, null);

        await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(Guid.NewGuid(), dto));
    }

    [Fact]
    public async Task UpdateProfileAsync_IdentityVerified_CannotChangeName()
    {
        var user = CreateTestUser();
        user.IsIdentityVerified = true;
        user.Name = "Original Name";
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var dto = new UpdateProfileDto("Changed Name", user.Email, user.Nif, user.CitizenCardNumber, null, null, null, null);

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));
        Assert.Contains("validação de identidade", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_InvalidNifLength_ThrowsException()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var dto = new UpdateProfileDto("Name", "test@test.com", "12345", null, null, null, null, null);

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));
        Assert.Contains("NIF", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_DuplicateNif_ThrowsException()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.IsEmailUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.IsNifUniqueAsync("123456789", user.Id)).ReturnsAsync(false);

        var dto = new UpdateProfileDto("Name", "test@test.com", "123456789", null, null, null, null, null);

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));
        Assert.Contains("NIF já está registado", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_InvalidCcLength_ThrowsException()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.IsEmailUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);
        _userRepoMock.Setup(r => r.IsNifUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);

        var dto = new UpdateProfileDto("Name", "test@test.com", "123456789", "12345", null, null, null, null);

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));
        Assert.Contains("Cartão de Cidadão", ex.Message);
    }

    // --- UpdatePasswordAsync ---

    [Fact]
    public async Task UpdatePasswordAsync_CorrectCurrentPassword_UpdatesHash()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        await _sut.UpdatePasswordAsync(user.Id, "OldPassword", "NewPassword123!");

        Assert.True(BCrypt.Net.BCrypt.Verify("NewPassword123!", user.PasswordHash));
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdatePasswordAsync_WrongCurrentPassword_ThrowsException()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.UpdatePasswordAsync(user.Id, "WrongPassword", "NewPass123!"));
        Assert.Contains("Password atual incorreta", ex.Message);
    }

    [Fact]
    public async Task UpdatePasswordAsync_UserNotFound_ThrowsException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        await Assert.ThrowsAsync<Exception>(
            () => _sut.UpdatePasswordAsync(Guid.NewGuid(), "old", "new"));
    }

    // --- UpdateAvatarAsync ---

    [Fact]
    public async Task UpdateAvatarAsync_ValidInput_UploadsAndUpdatesUrl()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _imageServiceMock.Setup(s => s.UploadImageAsync(It.IsAny<Stream>(), "avatar.jpg", "profiles"))
            .ReturnsAsync("https://cdn.example.com/profiles/avatar.webp");

        using var stream = new MemoryStream();
        var url = await _sut.UpdateAvatarAsync(user.Id, stream, "avatar.jpg");

        Assert.Equal("https://cdn.example.com/profiles/avatar.webp", url);
        Assert.Equal("https://cdn.example.com/profiles/avatar.webp", user.ProfilePictureUrl);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task UpdateAvatarAsync_UserNotFound_ThrowsException()
    {
        _userRepoMock.Setup(r => r.GetByIdAsync(It.IsAny<Guid>())).ReturnsAsync((User?)null);

        using var stream = new MemoryStream();
        await Assert.ThrowsAsync<Exception>(() => _sut.UpdateAvatarAsync(Guid.NewGuid(), stream, "test.jpg"));
    }

    // --- UpdateProfileAsync - Phone Validation ---

    [Fact]
    public async Task UpdateProfileAsync_PhoneWithoutCountryCode_ThrowsException()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var dto = new UpdateProfileDto("Name", "test@test.com", null, null, null, null, null, "+351912345678");

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));
        Assert.Contains("Seleciona o país", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_UnsupportedCountryCode_ThrowsException()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var dto = new UpdateProfileDto("Name", "test@test.com", null, null, null, null, "ZZ", "+999123456789");

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));
        Assert.Contains("não é suportado", ex.Message);
    }

    [Fact]
    public async Task UpdateProfileAsync_InvalidPhonePlatform_ThrowsException()
    {
        var user = CreateTestUser();
        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.IsEmailUniqueAsync(It.IsAny<string>(), user.Id)).ReturnsAsync(true);

        var dto = new UpdateProfileDto("Name", "test@test.com", null, null, null, null, null, null, "sms");

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.UpdateProfileAsync(user.Id, dto));
        Assert.Contains("plataforma de contacto telefónico", ex.Message);
    }

    [Fact]
    public async Task RequestPhoneNumberVerificationAsync_TelegramPlatform_UsesTelegramService()
    {
        var user = CreateTestUser();
        user.PhoneContactPlatform = PhoneContactPlatforms.Telegram;

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _userRepoMock.Setup(r => r.IsPhoneNumberUniqueAsync("+351912345678", user.Id)).ReturnsAsync(true);
        _telegramMessagingMock
            .Setup(service => service.StartPhoneVerificationAsync(user.Id, "+351912345678", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new TelegramPhoneVerificationStartResult(
                "Abre o Telegram.",
                DateTime.UtcNow.AddMinutes(10),
                "https://t.me/test_bot?start=abc",
                "test_bot",
                false));

        var sut = new UserService(
            _uowMock.Object,
            _imageServiceMock.Object,
            _geminiMock.Object,
            _contactAccessMock.Object,
            whatsAppCodeService: null,
            telegramMessagingPlatformService: _telegramMessagingMock.Object);

        var result = await sut.RequestPhoneNumberVerificationAsync(
            user.Id,
            new RequestPhoneVerificationDto("PT", "+351912345678", PhoneContactPlatforms.Telegram),
            null,
            null);

        Assert.Equal(PhoneContactPlatforms.Telegram, result.Platform);
        Assert.Equal("test_bot", result.BotUsername);
        Assert.Equal("https://t.me/test_bot?start=abc", result.DeepLinkUrl);
        Assert.Equal("+351912345678", user.PendingPhoneNumber);
        Assert.Equal("PT", user.PendingPhoneCountryCode);
        Assert.Equal(PhoneContactPlatforms.Telegram, user.PendingPhoneContactPlatform);
        _telegramMessagingMock.Verify(service => service.StartPhoneVerificationAsync(user.Id, "+351912345678", It.IsAny<CancellationToken>()), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RequestPhoneNumberVerificationAsync_VerifiedPhone_ThrowsWhenTryingDifferentPhone()
    {
        var user = CreateTestUser();
        user.PhoneCountryCode = "PT";
        user.PhoneNumber = "+351911111111";
        user.PhoneContactPlatform = PhoneContactPlatforms.Telegram;
        user.IsPhoneNumberVerified = true;

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<Exception>(() => _sut.RequestPhoneNumberVerificationAsync(
            user.Id,
            new RequestPhoneVerificationDto("PT", "+351922222222", PhoneContactPlatforms.Telegram),
            null,
            null,
            CancellationToken.None));

        Assert.Contains("telemóvel", ex.Message);
        _telegramMessagingMock.Verify(service => service.StartPhoneVerificationAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Never);
    }

    [Fact]
    public async Task VerifyPhoneNumberAsync_WhatsAppPendingPhone_PromotesPendingPhone()
    {
        var user = CreateTestUser();
        user.PhoneCountryCode = "PT";
        user.PhoneNumber = "+351911111111";
        user.PhoneContactPlatform = PhoneContactPlatforms.Telegram;
        user.IsPhoneNumberVerified = true;
        user.PendingPhoneCountryCode = "PT";
        user.PendingPhoneNumber = "+351922222222";
        user.PendingPhoneContactPlatform = PhoneContactPlatforms.WhatsApp;

        _userRepoMock.Setup(r => r.GetByIdAsync(user.Id)).ReturnsAsync(user);
        _whatsAppCodeServiceMock
            .Setup(service => service.VerifyPhoneVerificationCodeAsync(user.Id, "+351922222222", "123456", It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var sut = new UserService(
            _uowMock.Object,
            _imageServiceMock.Object,
            _geminiMock.Object,
            _contactAccessMock.Object,
            whatsAppCodeService: _whatsAppCodeServiceMock.Object,
            telegramMessagingPlatformService: null);

        await sut.VerifyPhoneNumberAsync(user.Id, "123456");

        Assert.Equal("+351922222222", user.PhoneNumber);
        Assert.Equal("PT", user.PhoneCountryCode);
        Assert.Equal(PhoneContactPlatforms.WhatsApp, user.PhoneContactPlatform);
        Assert.True(user.IsPhoneNumberVerified);
        Assert.Null(user.PendingPhoneNumber);
        Assert.Null(user.PendingPhoneCountryCode);
        Assert.Null(user.PendingPhoneContactPlatform);
        _whatsAppCodeServiceMock.Verify(
            service => service.VerifyPhoneVerificationCodeAsync(user.Id, "+351922222222", "123456", It.IsAny<CancellationToken>()),
            Times.Once);
    }
}
