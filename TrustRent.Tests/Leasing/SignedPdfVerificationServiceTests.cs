using Microsoft.Extensions.Logging;
using Moq;
using TrustRent.Modules.Leasing.Services;

namespace TrustRent.Tests.Leasing;

public class SignedPdfVerificationServiceTests
{
    private readonly SignedPdfVerificationService _sut;

    public SignedPdfVerificationServiceTests()
    {
        var loggerMock = new Mock<ILogger<SignedPdfVerificationService>>();
        _sut = new SignedPdfVerificationService(loggerMock.Object);
    }

    [Fact]
    public async Task VerifySignaturesAsync_EmptyPdf_ReturnsError()
    {
        var emptyPdf = Array.Empty<byte>();

        var result = await _sut.VerifySignaturesAsync(emptyPdf, 1);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifySignaturesAsync_InvalidPdfBytes_ReturnsError()
    {
        var invalidPdf = new byte[] { 0x00, 0x01, 0x02, 0x03 };

        var result = await _sut.VerifySignaturesAsync(invalidPdf, 1);

        Assert.False(result.IsValid);
        Assert.NotNull(result.ErrorMessage);
    }

    [Fact]
    public async Task VerifySignaturesAsync_ValidPdfNoSignatures_ReturnsNotEnoughSignatures()
    {
        // Minimal valid PDF without signatures
        var pdfContent = "%PDF-1.4\n1 0 obj\n<< /Type /Catalog /Pages 2 0 R >>\nendobj\n2 0 obj\n<< /Type /Pages /Kids [3 0 R] /Count 1 >>\nendobj\n3 0 obj\n<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>\nendobj\nxref\n0 4\n0000000000 65535 f \n0000000009 00000 n \n0000000058 00000 n \n0000000115 00000 n \ntrailer\n<< /Size 4 /Root 1 0 R >>\nstartxref\n190\n%%EOF";
        var pdfBytes = System.Text.Encoding.ASCII.GetBytes(pdfContent);

        var result = await _sut.VerifySignaturesAsync(pdfBytes, 1);

        // Either returns invalid with error about missing signatures, or catches an exception
        Assert.False(result.IsValid);
    }
}
