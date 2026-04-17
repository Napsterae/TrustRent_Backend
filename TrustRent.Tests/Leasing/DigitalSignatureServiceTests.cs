using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Leasing.Services;

namespace TrustRent.Tests.Leasing;

public class DigitalSignatureServiceTests
{
    private readonly DigitalSignatureService _sut;

    public DigitalSignatureServiceTests()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DigitalSignature:CMD:MockEnabled"] = "true"
            })
            .Build();

        var logger = new Mock<ILogger<DigitalSignatureService>>();
        _sut = new DigitalSignatureService(config, logger.Object);
    }

    [Fact]
    public async Task InitiateCmdSignatureAsync_ReturnsSuccessWithProcessId()
    {
        var result = await _sut.InitiateCmdSignatureAsync("hash123", "+351912345678", "user@test.com");

        Assert.True(result.Success);
        Assert.NotEmpty(result.ProcessId);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyCmdSignatureAsync_InvalidProcessId_ReturnsFailure()
    {
        var result = await _sut.VerifyCmdSignatureAsync("nonexistent", "123456");

        Assert.False(result.Success);
        Assert.Null(result.SignatureRef);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifyCmdSignatureAsync_WrongOtp_ReturnsFailure()
    {
        var initResult = await _sut.InitiateCmdSignatureAsync("hash123", "+351912345678", "user@test.com");
        
        var result = await _sut.VerifyCmdSignatureAsync(initResult.ProcessId, "000000");

        Assert.False(result.Success);
        Assert.Contains("OTP inválido", result.ErrorMessage);
    }

    [Fact]
    public async Task GetSignatureStatusAsync_CompletedProcess_ReturnsCompleted()
    {
        var result = await _sut.GetSignatureStatusAsync("nonexistent-process");

        Assert.Equal("Completed", result.State);
        Assert.NotNull(result.CompletedAt);
    }

    [Fact]
    public async Task GetSignatureStatusAsync_PendingProcess_ReturnsPending()
    {
        var initResult = await _sut.InitiateCmdSignatureAsync("hash", "+351912345678", "user@test.com");

        var status = await _sut.GetSignatureStatusAsync(initResult.ProcessId);

        Assert.Equal("Pending", status.State);
        Assert.Null(status.CompletedAt);
    }

    [Fact]
    public async Task MockDisabled_ThrowsNotImplementedException()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["DigitalSignature:CMD:MockEnabled"] = "false"
            })
            .Build();
        var logger = new Mock<ILogger<DigitalSignatureService>>();
        var service = new DigitalSignatureService(config, logger.Object);

        await Assert.ThrowsAsync<NotImplementedException>(
            () => service.InitiateCmdSignatureAsync("hash", "+351912345678", "user@test.com"));
    }
}
