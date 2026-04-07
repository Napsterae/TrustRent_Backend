namespace TrustRent.Modules.Catalog.Models;

public class Property
{
    public Guid Id { get; set; }
    public Guid LandlordId { get; set; } // Relacionamento (simulado) com o dono do imóvel
    public Guid? TenantId { get; set; }   // Relacionamento (simulado) com o inquilino atual

    // Informações Básicas
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string PropertyType { get; set; } = string.Empty;
    public string Typology { get; set; } = string.Empty;

    // Características
    public decimal Area { get; set; }
    public int Rooms { get; set; }
    public int Bathrooms { get; set; }
    public string Floor { get; set; } = string.Empty;

    // Comodidades (Toggles)
    public bool HasElevator { get; set; }
    public bool HasAirConditioning { get; set; }
    public bool HasGarage { get; set; }
    public bool AllowsPets { get; set; }
    public bool IsFurnished { get; set; }
    public string? FurnishedDescription { get; set; }

    // Localização
    public string District { get; set; } = string.Empty;
    public string Municipality { get; set; } = string.Empty;
    public string Parish { get; set; } = string.Empty;
    public string DoorNumber { get; set; } = string.Empty;
    public string Street { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Status da Plataforma
    public bool IsPublic { get; set; } = false;
    public bool IsUnderMaintenance { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Relacionamentos (As listas de imagens)
    public List<PropertyImage> Images { get; set; } = new();

    // Relacionamentos (Comodidades / Benefícios)
    public ICollection<PropertyAmenity> Amenities { get; set; } = new List<PropertyAmenity>();


    // Caderneta
    public string? MatrixArticle { get; set; }
    public string? PropertyFraction { get; set; }

    // Certificado Energético
    public string? EnergyClass { get; set; }
    public string? EnergyCertificateNumber { get; set; }
    public DateTime? EnergyCertificateExpiryDate { get; set; }

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

    // Contrato Oficial
    public bool HasOfficialContract { get; set; } = false;
}

// Classe para guardar a Galeria de Imagens
public class PropertyImage
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsMain { get; set; } // Diz se é a foto de Capa
}

