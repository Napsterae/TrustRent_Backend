using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Mappers;
using TrustRent.Modules.Catalog.Models;

namespace TrustRent.Tests.Catalog;

/// <summary>
/// Regression tests for PropertyMappers.ToEntity / UpdateEntity.
///
/// Bug context: the mapper previously dropped 6 document fields
/// (MatrixArticle, PropertyFraction, EnergyClass, EnergyCertificateNumber,
/// EnergyCertificateExpiryDate, AtRegistrationNumber) when persisting a
/// CreatePropertyDto. Simulated documents filled the DTO but never reached
/// the DB, so PropertyPassport showed them as "Pendente de validação".
/// </summary>
public class PropertyMappersTests
{
    private static CreatePropertyDto CreateDtoWithAllDocuments() => new()
    {
        Title = "Test",
        Description = "Descricao",
        Price = 500m,
        PropertyType = "Apartamento",
        Typology = "T1",
        Area = 50m,
        Rooms = 1,
        Bathrooms = 1,
        Floor = "1",
        District = "Lisboa",
        Municipality = "Lisboa",
        Parish = "Arroios",
        DoorNumber = "1",
        Street = "Rua Teste",
        PostalCode = "1000-001",
        AdvanceRentMonths = 0,
        LeaseRegime = "PermanentHousing",

        // Caderneta Predial
        MatrixArticle = "1234",
        PropertyFraction = "A",
        ParishConcelho = "União das Freguesias de Lisboa / Lisboa",

        // Certificado Energético
        EnergyClass = "B",
        EnergyCertificateNumber = "CE-2025-0001234",
        EnergyCertificateExpiryDate = "2030-12-31",

        // Registo AT
        AtRegistrationNumber = "AT-DEV-00000001",

        // Certidão Permanente
        PermanentCertNumber = "PC-DEV-1234-5678-9012",
        PermanentCertOffice = "Conservatória do Registo Predial de Lisboa",

        // Licença de Utilização
        UsageLicenseNumber = "LU-2024-0001",
        UsageLicenseDate = "2024-01-15",
        UsageLicenseIssuer = "Câmara Municipal de Lisboa",
    };

    // --- ToEntity: every document field must survive the mapping ---

    [Fact]
    public void ToEntity_MapsAllDocumentFields()
    {
        var dto = CreateDtoWithAllDocuments();
        var landlordId = Guid.NewGuid();

        var property = dto.ToEntity(landlordId);

        // Caderneta Predial
        Assert.Equal(dto.MatrixArticle, property.MatrixArticle);
        Assert.Equal(dto.PropertyFraction, property.PropertyFraction);
        Assert.Equal(dto.ParishConcelho, property.ParishConcelho);

        // Certificado Energético
        Assert.Equal(dto.EnergyClass, property.EnergyClass);
        Assert.Equal(dto.EnergyCertificateNumber, property.EnergyCertificateNumber);
        Assert.NotNull(property.EnergyCertificateExpiryDate);

        // Registo AT
        Assert.Equal(dto.AtRegistrationNumber, property.AtRegistrationNumber);

        // Certidão Permanente
        Assert.Equal(dto.PermanentCertNumber, property.PermanentCertNumber);
        Assert.Equal(dto.PermanentCertOffice, property.PermanentCertOffice);

        // Licença de Utilização
        Assert.Equal(dto.UsageLicenseNumber, property.UsageLicenseNumber);
        Assert.Equal(dto.UsageLicenseDate, property.UsageLicenseDate);
        Assert.Equal(dto.UsageLicenseIssuer, property.UsageLicenseIssuer);
    }

    [Fact]
    public void ToEntity_WithNullDocuments_LeavesDocumentFieldsNull()
    {
        var dto = CreateDtoWithAllDocuments();
        // Wipe every document field — a freshly created property with no docs yet.
        dto.MatrixArticle = null;
        dto.PropertyFraction = null;
        dto.ParishConcelho = null;
        dto.EnergyClass = null;
        dto.EnergyCertificateNumber = null;
        dto.EnergyCertificateExpiryDate = null;
        dto.AtRegistrationNumber = null;
        dto.PermanentCertNumber = null;
        dto.PermanentCertOffice = null;
        dto.UsageLicenseNumber = null;
        dto.UsageLicenseDate = null;
        dto.UsageLicenseIssuer = null;

        var property = dto.ToEntity(Guid.NewGuid());

        Assert.Null(property.MatrixArticle);
        Assert.Null(property.PropertyFraction);
        Assert.Null(property.ParishConcelho);
        Assert.Null(property.EnergyClass);
        Assert.Null(property.EnergyCertificateNumber);
        Assert.Null(property.EnergyCertificateExpiryDate);
        Assert.Null(property.AtRegistrationNumber);
        Assert.Null(property.PermanentCertNumber);
        Assert.Null(property.PermanentCertOffice);
        Assert.Null(property.UsageLicenseNumber);
        Assert.Null(property.UsageLicenseDate);
        Assert.Null(property.UsageLicenseIssuer);
    }

    // --- UpdateEntity: every document field must be written onto the existing entity ---

    [Fact]
    public void UpdateEntity_OverwritesAllDocumentFields()
    {
        var dto = CreateDtoWithAllDocuments();
        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            // Pre-existing stale values that the update must replace.
            MatrixArticle = "OLD",
            PropertyFraction = "OLD",
            ParishConcelho = "OLD",
            EnergyClass = "F",
            EnergyCertificateNumber = "OLD",
            EnergyCertificateExpiryDate = new DateTime(2000, 1, 1),
            AtRegistrationNumber = "OLD",
            PermanentCertNumber = "OLD",
            PermanentCertOffice = "OLD",
            UsageLicenseNumber = "OLD",
            UsageLicenseDate = "OLD",
            UsageLicenseIssuer = "OLD",
        };

        dto.UpdateEntity(property);

        Assert.Equal(dto.MatrixArticle, property.MatrixArticle);
        Assert.Equal(dto.PropertyFraction, property.PropertyFraction);
        Assert.Equal(dto.ParishConcelho, property.ParishConcelho);
        Assert.Equal(dto.EnergyClass, property.EnergyClass);
        Assert.Equal(dto.EnergyCertificateNumber, property.EnergyCertificateNumber);
        Assert.NotNull(property.EnergyCertificateExpiryDate);
        Assert.Equal(dto.AtRegistrationNumber, property.AtRegistrationNumber);
        Assert.Equal(dto.PermanentCertNumber, property.PermanentCertNumber);
        Assert.Equal(dto.PermanentCertOffice, property.PermanentCertOffice);
        Assert.Equal(dto.UsageLicenseNumber, property.UsageLicenseNumber);
        Assert.Equal(dto.UsageLicenseDate, property.UsageLicenseDate);
        Assert.Equal(dto.UsageLicenseIssuer, property.UsageLicenseIssuer);
    }

    [Fact]
    public void UpdateEntity_WithNullDocuments_ClearsDocumentFields()
    {
        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = Guid.NewGuid(),
            MatrixArticle = "1234",
            PropertyFraction = "A",
            ParishConcelho = "Lisboa",
            EnergyClass = "B",
            EnergyCertificateNumber = "CE-1",
            EnergyCertificateExpiryDate = new DateTime(2030, 1, 1),
            AtRegistrationNumber = "AT-1",
            PermanentCertNumber = "PC-1",
            PermanentCertOffice = "Office",
            UsageLicenseNumber = "LU-1",
            UsageLicenseDate = "2024-01-01",
            UsageLicenseIssuer = "CML",
        };

        var dto = CreateDtoWithAllDocuments();
        dto.MatrixArticle = null;
        dto.PropertyFraction = null;
        dto.ParishConcelho = null;
        dto.EnergyClass = null;
        dto.EnergyCertificateNumber = null;
        dto.EnergyCertificateExpiryDate = null;
        dto.AtRegistrationNumber = null;
        dto.PermanentCertNumber = null;
        dto.PermanentCertOffice = null;
        dto.UsageLicenseNumber = null;
        dto.UsageLicenseDate = null;
        dto.UsageLicenseIssuer = null;

        dto.UpdateEntity(property);

        Assert.Null(property.MatrixArticle);
        Assert.Null(property.PropertyFraction);
        Assert.Null(property.ParishConcelho);
        Assert.Null(property.EnergyClass);
        Assert.Null(property.EnergyCertificateNumber);
        Assert.Null(property.EnergyCertificateExpiryDate);
        Assert.Null(property.AtRegistrationNumber);
        Assert.Null(property.PermanentCertNumber);
        Assert.Null(property.PermanentCertOffice);
        Assert.Null(property.UsageLicenseNumber);
        Assert.Null(property.UsageLicenseDate);
        Assert.Null(property.UsageLicenseIssuer);
    }

    // --- EnergyCertificateExpiryDate string -> DateTime? parsing ---

    [Theory]
    [InlineData("2030-12-31", 2030, 12, 31)]
    [InlineData("2030-12-31T00:00:00", 2030, 12, 31)]
    public void ToEntity_ParsesValidEnergyCertificateExpiryDate(string input, int year, int month, int day)
    {
        var dto = CreateDtoWithAllDocuments();
        dto.EnergyCertificateExpiryDate = input;

        var property = dto.ToEntity(Guid.NewGuid());

        Assert.Equal(new DateTime(year, month, day), property.EnergyCertificateExpiryDate);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-a-date")]
    public void ToEntity_WithInvalidEnergyCertificateExpiryDate_LeavesNull(string? input)
    {
        var dto = CreateDtoWithAllDocuments();
        dto.EnergyCertificateExpiryDate = input;

        var property = dto.ToEntity(Guid.NewGuid());

        Assert.Null(property.EnergyCertificateExpiryDate);
    }
}
