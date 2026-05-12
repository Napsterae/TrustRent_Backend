namespace TrustRent.Modules.Catalog.Contracts.DTOs;

// Modelo para receber os filtros do Frontend
public class PropertySearchQuery
{
    public string? SearchTerm { get; set; }
    public string? Type { get; set; }
    public string? Typologies { get; set; }
    public string? Sort { get; set; }
    public decimal? MinPrice { get; set; }
    public decimal? MaxPrice { get; set; }
    public string? Locations { get; set; }

    // Comodidades
    public bool? HasElevator { get; set; }
    public bool? HasAirConditioning { get; set; }
    public bool? HasGarage { get; set; }
    public bool? AllowsPets { get; set; }
    public bool? IsFurnished { get; set; }

    // Contrato
    public bool? HasOfficialContract { get; set; }

    // Paginação para o Scroll Infinito. Nullable para o binder não falhar quando
    // o cliente omite a paginação; os defaults continuam a ser aplicados no backend.
    public int? Page { get; set; }
    public int? PageSize { get; set; }

    public int EffectivePage
    {
        get
        {
            var page = Page.GetValueOrDefault(1);
            return page < 1 ? 1 : page;
        }
    }

    public int EffectivePageSize
    {
        get
        {
            var pageSize = PageSize.GetValueOrDefault(9);
            return pageSize < 1 ? 9 : pageSize;
        }
    }

    public string EffectiveSort
    {
        get
        {
            return Sort?.Trim().ToLowerInvariant() switch
            {
                "price_asc" or "priceasc" => "price_asc",
                "price_desc" or "pricedesc" => "price_desc",
                _ => "recent"
            };
        }
    }
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
    string MainImageUrl,
    bool HasOfficialContract,
    bool IsAvailable
);