using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Services;

public class PropertyService : IPropertyService
{
    private readonly CatalogDbContext _context;
    private readonly IImageService _imageService;

    public PropertyService(CatalogDbContext context, IImageService imageService)
    {
        _context = context;
        _imageService = imageService;
    }

    public async Task<Guid> CreatePropertyAsync(Guid landlordId, CreatePropertyDto dto, IEnumerable<FileDto> images, IEnumerable<FileDto> documents)
    {
        // 1. Mapear o DTO para o nosso Modelo da Base de Dados
        var property = new Property
        {
            Id = Guid.NewGuid(),
            LandlordId = landlordId,
            Title = dto.Title,
            Description = dto.Description,
            Price = dto.Price,
            PropertyType = dto.PropertyType,
            Typology = dto.Typology,
            Area = dto.Area,
            Rooms = dto.Rooms,
            Bathrooms = dto.Bathrooms,
            Floor = dto.Floor,
            HasElevator = dto.HasElevator,
            HasAirConditioning = dto.HasAirConditioning,
            HasGarage = dto.HasGarage,
            AllowsPets = dto.AllowsPets,
            IsFurnished = dto.IsFurnished,
            FurnishedDescription = dto.FurnishedDescription,
            Street = dto.Street,
            PostalCode = dto.PostalCode,
            City = dto.City,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            IsAvailable = false // Por defeito, fica como rascunho/em análise
        };

        // 2. Processar e fazer Upload das Imagens
        bool isFirstImage = true;
        foreach (var img in images)
        {
            if (img.Content.Length > 0)
            {
                // Aqui chamamos o serviço do módulo Shared. 
                // Ele vai automaticamente converter para WebP e devolver o URL da Cloud!
                var imageUrl = await _imageService.UploadImageAsync(img.Content, img.FileName, $"properties/{property.Id}");

                property.Images.Add(new PropertyImage
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    Url = imageUrl,
                    Category = "Geral",
                    IsMain = isFirstImage // A primeira imagem que vem no array fica como Capa
                });

                isFirstImage = false;
            }
        }

        // 3. Processar Documentos (Caderneta, etc)
        // (Futuramente podemos usar uma pasta diferente no Cloudinary ou S3 para PDFs)
        foreach (var doc in documents)
        {
            if (doc.Content.Length > 0)
            {
                var docUrl = await _imageService.UploadImageAsync(doc.Content, doc.FileName, $"properties/{property.Id}/docs");

                property.Documents.Add(new PropertyDocument
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    Url = docUrl,
                    DocumentType = "Documento" // Pode ser extraído no Frontend depois
                });
            }
        }

        // 4. Guardar tudo na Base de Dados
        _context.Properties.Add(property);
        await _context.SaveChangesAsync();

        return property.Id; // Devolvemos o ID para o Frontend poder redirecionar o utilizador
    }

    public async Task<IEnumerable<PropertySummaryDto>> GetPropertiesByLandlordAsync(Guid landlordId)
    {
        var properties = await _context.Properties
            .Include(p => p.Images) // Precisamos das imagens para ir buscar a capa
            .Where(p => p.LandlordId == landlordId)
            .OrderByDescending(p => p.CreatedAt)
            .Select(p => new PropertySummaryDto(
                p.Id,
                p.Title,
                p.City,
                p.IsAvailable,
                p.Images.FirstOrDefault(i => i.IsMain) != null
                    ? p.Images.FirstOrDefault(i => i.IsMain)!.Url
                    : "" // Fallback caso não tenha imagem (embora no teu create seja obrigatório)
            ))
            .ToListAsync();

        return properties;
    }
}