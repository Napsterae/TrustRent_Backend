using Moq;
using Microsoft.Extensions.Configuration;
using TrustRent.Shared.Security;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;
using TrustRent.Modules.Identity.Services;

namespace TrustRent.Tests.Identity;

public class AuthServiceTests
{
    private readonly Mock<IUnitOfWork> _uowMock;
    private readonly Mock<IUserRepository> _userRepoMock;
    private readonly IConfiguration _config;
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        _uowMock = new Mock<IUnitOfWork>();
        _userRepoMock = new Mock<IUserRepository>();
        _uowMock.Setup(u => u.Users).Returns(_userRepoMock.Object);

        var inMemorySettings = new Dictionary<string, string?>
        {
            ["JwtSettings:SecretKey"] = "SuperSecretKeyForTestingPurposesOnly1234567890!",
            ["JwtSettings:Issuer"] = "TrustRent.Tests",
            ["JwtSettings:Audience"] = "TrustRent.Tests"
        };
        _config = new ConfigurationBuilder()
            .AddInMemoryCollection(inMemorySettings)
            .Build();

        _sut = new AuthService(_uowMock.Object, _config);
    }

    [Fact]
    public async Task SignInWithEmailAsync_ExistingUser_ReturnsJwtToken()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Email = "test@example.com",
            PasswordHash = string.Empty,
            TrustScore = 50
        };
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        var token = await _sut.SignInWithEmailAsync("test@example.com");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task SignInWithEmailAsync_NewUser_CreatesAccount_AndReturnsToken()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("new.user@example.com"))
            .ReturnsAsync((User?)null);

        User? capturedUser = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(user => capturedUser = user);

        var token = await _sut.SignInWithEmailAsync("new.user@example.com");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        Assert.NotNull(capturedUser);
        Assert.Equal("new.user@example.com", capturedUser!.Email);
        Assert.Equal("New User", capturedUser.Name);
        Assert.Equal(string.Empty, capturedUser.PasswordHash);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task SignInWithEmailAsync_DifferentCaseEmail_ReturnsToken()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Email = "test@example.com",
            PasswordHash = string.Empty,
            TrustScore = 50
        };
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        var token = await _sut.SignInWithEmailAsync("TEST@EXAMPLE.COM");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task SignInWithEmailAsync_GeneratedTokenContainsUserClaims()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "João Silva",
            Email = "joao@example.com",
            PasswordHash = string.Empty,
            TrustScore = 50
        };
        _userRepoMock.Setup(r => r.GetByEmailAsync("joao@example.com"))
            .ReturnsAsync(user);

        var token = await _sut.SignInWithEmailAsync("joao@example.com");

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("joao@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("João Silva", jwt.Claims.First(c => c.Type == "name").Value);
    }

    [Fact]
    public void EmailHelper_RemoveDiacritics_HandlesPortugueseChars()
    {
        Assert.Equal("ao", EmailHelper.RemoveDiacritics("ão"));
        Assert.Equal("ca", EmailHelper.RemoveDiacritics("ça"));
        Assert.Equal("ca", EmailHelper.RemoveDiacritics("çã"));
        Assert.Equal("pao", EmailHelper.RemoveDiacritics("pão"));
        Assert.Equal("paes", EmailHelper.RemoveDiacritics("pães"));
    }

    [Fact]
    public void EmailHelper_NormalizeEmail_RemovesDiacriticsAndLowercases()
    {
        Assert.Equal("joao@email.com", EmailHelper.NormalizeEmail("João@email.com"));
        Assert.Equal("joao@email.com", EmailHelper.NormalizeEmail("joÃO@email.COM"));
        Assert.Equal("paos@email.com", EmailHelper.NormalizeEmail("PÃoS@email.com"));
    }
}
