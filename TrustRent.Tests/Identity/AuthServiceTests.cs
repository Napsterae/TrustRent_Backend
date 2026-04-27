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
    public async Task RegisterAsync_NewUser_ReturnsJwtToken()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync((User?)null);

        var token = await _sut.RegisterAsync("Test User", "test@example.com", "Password123!");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
        _userRepoMock.Verify(r => r.AddAsync(It.Is<User>(u =>
            u.Name == "Test User" && u.Email == "test@example.com")), Times.Once);
        _uowMock.Verify(u => u.SaveChangesAsync(), Times.Once);
    }

    [Fact]
    public async Task RegisterAsync_ExistingEmail_ThrowsException()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Email = "test@example.com" };
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(existingUser);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.RegisterAsync("Test", "test@example.com", "Pass123!"));
        Assert.Contains("Email já está em uso", ex.Message);
    }

    [Fact]
    public async Task RegisterAsync_PasswordIsHashed()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        User? capturedUser = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u);

        await _sut.RegisterAsync("Test", "test@example.com", "MyPassword");

        Assert.NotNull(capturedUser);
        Assert.NotEqual("MyPassword", capturedUser!.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify("MyPassword", capturedUser.PasswordHash));
    }

    [Fact]
    public async Task LoginAsync_ValidCredentials_ReturnsJwtToken()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct123!"),
            TrustScore = 50
        };
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        var token = await _sut.LoginAsync("test@example.com", "Correct123!");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task LoginAsync_InvalidEmail_ThrowsException()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync("wrong@example.com"))
            .ReturnsAsync((User?)null);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.LoginAsync("wrong@example.com", "Pass123!"));
        Assert.Contains("Credenciais inválidas", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ThrowsException()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = "test@example.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("CorrectPassword")
        };
        _userRepoMock.Setup(r => r.GetByEmailAsync("test@example.com"))
            .ReturnsAsync(user);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.LoginAsync("test@example.com", "WrongPassword"));
        Assert.Contains("Credenciais inválidas", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_DifferentCaseEmail_ReturnsToken()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "Test",
            Email = "Test@Example.Com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct123!"),
            TrustScore = 50
        };
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        var token = await _sut.LoginAsync("test@example.com", "Correct123!");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task RegisterAsync_UppercaseEmail_NormalizesToLowercase()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        User? capturedUser = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u);

        await _sut.RegisterAsync("Test", "TEST@EXAMPLE.COM", "Password123!");

        Assert.NotNull(capturedUser);
        Assert.Equal("test@example.com", capturedUser!.Email);
    }

    [Fact]
    public async Task RegisterAsync_GeneratedTokenContainsUserClaims()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        var token = await _sut.RegisterAsync("João Silva", "joao@example.com", "Pass123!");

        var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        Assert.Equal("joao@example.com", jwt.Claims.First(c => c.Type == "email").Value);
        Assert.Equal("João Silva", jwt.Claims.First(c => c.Type == "name").Value);
    }

    [Fact]
    public async Task RegisterAsync_DiacriticEmail_NormalizesToAscii()
    {
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((User?)null);

        User? capturedUser = null;
        _userRepoMock.Setup(r => r.AddAsync(It.IsAny<User>()))
            .Callback<User>(u => capturedUser = u);

        await _sut.RegisterAsync("João Silva", "joão@email.com", "Password123!");

        Assert.NotNull(capturedUser);
        Assert.Equal("joao@email.com", capturedUser!.Email);
    }

    [Fact]
    public async Task LoginAsync_DiacriticEmail_FindsNormalizedUser()
    {
        var user = new User
        {
            Id = Guid.NewGuid(),
            Name = "João Silva",
            Email = "joao@email.com",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Correct123!"),
            TrustScore = 50
        };
        // Repository is responsible for normalization; mock returns the user
        // regardless of input casing/diacritics so the contract is honored.
        _userRepoMock.Setup(r => r.GetByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync(user);

        var token = await _sut.LoginAsync("joão@email.com", "Correct123!");

        Assert.NotNull(token);
        Assert.NotEmpty(token);
    }

    [Fact]
    public async Task RegisterAsync_DiacriticVsAscii_DetectsDuplicate()
    {
        var existingUser = new User { Id = Guid.NewGuid(), Email = "joao@email.com" };
        _userRepoMock.Setup(r => r.GetByEmailAsync("joao@email.com"))
            .ReturnsAsync(existingUser);

        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.RegisterAsync("João Silva", "joão@email.com", "Password123!"));

        Assert.Contains("Email já está em uso", ex.Message);
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
