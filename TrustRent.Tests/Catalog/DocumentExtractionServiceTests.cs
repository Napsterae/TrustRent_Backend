using Moq;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Services;
using TrustRent.Shared.Contracts.Interfaces;
using TrustRent.Shared.Models.DocumentExtraction;
using TrustRent.Shared.Services;

namespace TrustRent.Tests.Catalog;

public class DocumentExtractionServiceTests
{
    private readonly Mock<IGeminiDocumentService> _geminiMock;
    private readonly DocumentExtractionService _sut;

    public DocumentExtractionServiceTests()
    {
        _geminiMock = new Mock<IGeminiDocumentService>();
        _sut = new DocumentExtractionService(_geminiMock.Object);
    }

    [Fact]
    public async Task ExtractDataAsync_UnsupportedDocType_ThrowsException()
    {
        using var stream = new MemoryStream();

        await Assert.ThrowsAsync<ArgumentException>(
            () => _sut.ExtractDataAsync(stream, "file.pdf", "unknown_type"));
    }

    [Fact]
    public async Task ExtractDataAsync_Caderneta_ReturnsCorrectData()
    {
        var response = new CadernetaPredialResponse
        {
            IsAuthentic = true,
            AllFieldsExtracted = true,
            ImageQuality = "good",
            MatrixArticle = "1234",
            PropertyFraction = "A",
            ParishConcelho = "Lisboa"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<CadernetaPredialResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var result = await _sut.ExtractDataAsync(stream, "caderneta.pdf", "caderneta");

        Assert.Equal("1234", result.MatrixArticle);
        Assert.Equal("A", result.PropertyFraction);
        Assert.Equal("Lisboa", result.ParishConcelho);
    }

    [Fact]
    public async Task ExtractDataAsync_Certificado_ReturnsCorrectData()
    {
        var response = new CertificadoEnergeticoResponse
        {
            IsAuthentic = true,
            AllFieldsExtracted = true,
            ImageQuality = "good",
            EnergyClass = "A+",
            EnergyCertNumber = "CE-12345"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<CertificadoEnergeticoResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var result = await _sut.ExtractDataAsync(stream, "cert.pdf", "certificado");

        Assert.Equal("A+", result.EnergyClass);
        Assert.Equal("CE-12345", result.EnergyCertNumber);
    }

    [Fact]
    public async Task ExtractDataAsync_RegistoAt_ReturnsCorrectData()
    {
        var response = new RegistoAtResponse
        {
            IsAuthentic = true,
            AllFieldsExtracted = true,
            ImageQuality = "good",
            AtRegistrationNumber = "AT-9999"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<RegistoAtResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var result = await _sut.ExtractDataAsync(stream, "at.pdf", "modelo2");

        Assert.Equal("AT-9999", result.AtRegistrationNumber);
    }

    [Fact]
    public async Task ExtractDataAsync_Certidao_ReturnsCorrectData()
    {
        var response = new CertidaoPermanenteResponse
        {
            IsAuthentic = true,
            AllFieldsExtracted = true,
            ImageQuality = "good",
            PermanentCertNumber = "PC-111",
            PermanentCertOffice = "Conservatória Lisboa"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<CertidaoPermanenteResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var result = await _sut.ExtractDataAsync(stream, "certidao.pdf", "certidao");

        Assert.Equal("PC-111", result.PermanentCertNumber);
        Assert.Equal("Conservatória Lisboa", result.PermanentCertOffice);
    }

    [Fact]
    public async Task ExtractDataAsync_Licenca_ReturnsCorrectData()
    {
        var response = new LicencaUtilizacaoResponse
        {
            IsAuthentic = true,
            AllFieldsExtracted = true,
            ImageQuality = "good",
            LicenseNumber = "LU-555",
            LicenseDate = "2024-01-15",
            LicenseIssuer = "Câmara Municipal de Lisboa"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<LicencaUtilizacaoResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var result = await _sut.ExtractDataAsync(stream, "licenca.pdf", "licenca");

        Assert.Equal("LU-555", result.LicenseNumber);
        Assert.Equal("2024-01-15", result.LicenseDate);
        Assert.Equal("Câmara Municipal de Lisboa", result.LicenseIssuer);
    }

    [Fact]
    public async Task ExtractDataAsync_NotAuthentic_ThrowsException()
    {
        var response = new CadernetaPredialResponse
        {
            IsAuthentic = false,
            AllFieldsExtracted = true,
            ImageQuality = "good"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<CadernetaPredialResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.ExtractDataAsync(stream, "fake.pdf", "caderneta"));
        Assert.Contains("autenticidade", ex.Message);
    }

    [Fact]
    public async Task ExtractDataAsync_BlurryImage_ThrowsException()
    {
        var response = new CadernetaPredialResponse
        {
            IsAuthentic = true,
            AllFieldsExtracted = true,
            ImageQuality = "blurry"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<CadernetaPredialResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.ExtractDataAsync(stream, "blurry.pdf", "caderneta"));
        Assert.Contains("desfocada", ex.Message);
    }

    [Fact]
    public async Task ExtractDataAsync_FieldsNotExtracted_ThrowsException()
    {
        var response = new CadernetaPredialResponse
        {
            IsAuthentic = true,
            AllFieldsExtracted = false,
            ImageQuality = "good"
        };
        _geminiMock.Setup(g => g.ExtractDocumentAsync<CadernetaPredialResponse>(
            It.IsAny<Stream>(), It.IsAny<string>(), It.IsAny<string>()))
            .ReturnsAsync(response);

        using var stream = new MemoryStream();
        var ex = await Assert.ThrowsAsync<Exception>(
            () => _sut.ExtractDataAsync(stream, "incomplete.pdf", "caderneta"));
        Assert.Contains("extrair toda a informação", ex.Message);
    }
}
