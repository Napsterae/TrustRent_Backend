namespace TrustRent.Modules.Catalog.Contracts.DTOs;

// Modelo para receber os filtros do Frontend
public class PropertySearchQuery
{
    public string? SearchTerm { get; set; }
    public string? Type { get; set; }
    public string? Typologies { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Locations { get; set; }

    // Comodidades
    public bool? HasElevator { get; set; }
    public bool? HasAirConditioning { get; set; }
    public bool? HasGarage { get; set; }
    public bool? AllowsPets { get; set; }
    public bool? IsFurnished { get; set; }

    // Paginação para o Scroll Infinito
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 9; // 9 fica bem numa grelha de 3 colunas
}

// Resposta genérica paginada
public class PagedResult<T>
{
    public IEnumerable<T> Items { get; set; } = new List<T>();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
    public bool HasNextPage => Page * PageSize < TotalCount;
}

// DTO Específico para o Card de Pesquisa (Evita enviar dados desnecessários)
public record PropertySearchDto(
    Guid Id,
    string Title,
    string Municipality,
    string Parish,
    decimal Price,
    string PropertyType,
    string Typology,
    decimal Area,
    int Rooms,
    int Bathrooms,
    bool AllowsPets,
    string MainImageUrl
);