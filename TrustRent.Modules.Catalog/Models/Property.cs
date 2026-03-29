namespace TrustRent.Modules.Catalog.Models;

public class Property
{
    public Guid Id { get; set; }
    public Guid LandlordId { get; set; } // Relacionamento (simulado) com o dono do imóvel

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
    public string Street { get; set; } = string.Empty;
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }

    // Status da Plataforma
    public bool IsAvailable { get; set; } = false; // Se está público ou é rascunho
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }

    // Relacionamentos (As listas de imagens e documentos)
    public List<PropertyImage> Images { get; set; } = new();
    public List<PropertyDocument> Documents { get; set; } = new();
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

// Classe para guardar os Documentos (Caderneta, etc)
public class PropertyDocument
{
    public Guid Id { get; set; }
    public Guid PropertyId { get; set; }
    public string DocumentType { get; set; } = string.Empty; // Ex: CadernetaPredial, CertificadoEnergetico
    public string Url { get; set; } = string.Empty;
}

