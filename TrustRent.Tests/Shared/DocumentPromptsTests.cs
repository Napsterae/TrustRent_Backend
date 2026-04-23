using TrustRent.Shared.Services;

namespace TrustRent.Tests.Shared;

public class DocumentPromptsTests
{
    [Theory]
    [InlineData("caderneta")]
    [InlineData("certificado")]
    [InlineData("modelo2")]
    [InlineData("certidao")]
    [InlineData("licenca")]
    [InlineData("recibo")]
    public void GetPromptForDocType_ValidType_ReturnsNonEmptyPrompt(string docType)
    {
        var result = DocumentPrompts.GetPromptForDocType(docType);

        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GetPromptForDocType_Caderneta_ContainsExpectedFields()
    {
        var result = DocumentPrompts.GetPromptForDocType("caderneta");

        Assert.Contains("matrixArticle", result);
        Assert.Contains("propertyFraction", result);
        Assert.Contains("parishConcelho", result);
    }

    [Fact]
    public void GetPromptForDocType_Certificado_ContainsExpectedFields()
    {
        var result = DocumentPrompts.GetPromptForDocType("certificado");

        Assert.Contains("energyClass", result);
        Assert.Contains("energyCertNumber", result);
    }

    [Fact]
    public void GetPromptForDocType_Modelo2_ContainsExpectedFields()
    {
        var result = DocumentPrompts.GetPromptForDocType("modelo2");

        Assert.Contains("atRegistrationNumber", result);
    }

    [Fact]
    public void GetPromptForDocType_Certidao_ContainsExpectedFields()
    {
        var result = DocumentPrompts.GetPromptForDocType("certidao");

        Assert.Contains("permanentCertNumber", result);
        Assert.Contains("permanentCertOffice", result);
    }

    [Fact]
    public void GetPromptForDocType_Licenca_ContainsExpectedFields()
    {
        var result = DocumentPrompts.GetPromptForDocType("licenca");

        Assert.Contains("licenseNumber", result);
        Assert.Contains("licenseDate", result);
        Assert.Contains("licenseIssuer", result);
    }

    [Fact]
    public void GetPromptForDocType_Recibo_ContainsExpectedFields()
    {
        var result = DocumentPrompts.GetPromptForDocType("recibo");

        Assert.Contains("employeeName", result);
        Assert.Contains("employeeNif", result);
        Assert.Contains("netSalary", result);
        Assert.Contains("grossSalary", result);
        Assert.Contains("referenceMonth", result);
    }

    [Theory]
    [InlineData("")]
    [InlineData("invalid_type")]
    public void GetPromptForDocType_InvalidType_ThrowsArgumentException(string docType)
    {
        Assert.Throws<ArgumentException>(() => DocumentPrompts.GetPromptForDocType(docType));
    }

    [Fact]
    public void AllPrompts_ContainCommonInstructions()
    {
        var prompts = new[]
        {
            DocumentPrompts.CadernetaPredial,
            DocumentPrompts.CertificadoEnergetico,
            DocumentPrompts.RegistoAt,
            DocumentPrompts.CertidaoPermanente,
            DocumentPrompts.LicencaUtilizacao,
            DocumentPrompts.ReciboVencimento
        };

        foreach (var prompt in prompts)
        {
            Assert.Contains("isAuthentic", prompt);
            Assert.Contains("imageQuality", prompt);
            Assert.Contains("allFieldsExtracted", prompt);
        }
    }
}
