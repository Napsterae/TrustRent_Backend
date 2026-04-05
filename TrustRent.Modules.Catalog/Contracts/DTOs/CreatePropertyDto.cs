namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class CreatePropertyDto
{
    public bool IsPublic { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PropertyType { get; set; } = string.Empty;
    public string Typology { get; set; } = string.Empty;
    public decimal Area { get; set; }
    public int Rooms { get; set; }
    public int Bathrooms { get; set; }
    public string Floor { get; set; } = string.Empty;

    // Comodidades
    public bool HasElevator { get; set; }
    public bool HasAirConditioning { get; set; }
    public bool HasGarage { get; set; }
    public bool AllowsPets { get; set; }
    public bool IsFurnished { get; set; }
    public string? FurnishedDescription { get; set; }

    // Localização
    public string Street { get; set; } = string.Empty;
    public string District { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string Parish { get; set; } = string.Empty;
    public string DoorNumber { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Caderneta
    public string? MatrixArticle { get; set; }
    public string? PropertyFraction { get; set; }

    // Certificado Energético
    public string? EnergyClass { get; set; }
    public string? EnergyCertificateNumber { get; set; }
    public string? EnergyCertificateExpiryDate { get; set; }

    // Registo AT
    public string? AtRegistrationNumber { get; set; }

    // Caderneta - Freguesia/Concelho
    public string? ParishConcelho { get; set; }

    // Certidão Permanente
    public string? PermanentCertNumber { get; set; }
    public string? PermanentCertOffice { get; set; }

    // Licença de Utilização
    public string? UsageLicenseNumber { get; set; }
    public string? UsageLicenseDate { get; set; }
    public string? UsageLicenseIssuer { get; set; }

    // Caução e Despesas
    public decimal? Deposit { get; set; }
    public string CondominiumFeesPaidBy { get; set; } = "Inquilino";
    public string WaterPaidBy { get; set; } = "Inquilino";
    public string ElectricityPaidBy { get; set; } = "Inquilino";
    public string GasPaidBy { get; set; } = "Inquilino";
}

// Pequeno record para transportar os ficheiros do Endpoint para o Serviço sem quebrar o Clean Architecture
public record FileDto(Stream Content, string FileName);