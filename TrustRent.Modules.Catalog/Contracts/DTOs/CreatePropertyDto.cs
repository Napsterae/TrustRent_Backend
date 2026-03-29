namespace TrustRent.Modules.Catalog.Contracts.DTOs;

public class CreatePropertyDto
{
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
    public string PostalCode { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public double Latitude { get; set; }
    public double Longitude { get; set; }
}

// Pequeno record para transportar os ficheiros do Endpoint para o Serviço sem quebrar o Clean Architecture
public record FileDto(Stream Content, string FileName);