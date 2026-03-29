using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;
using Hangfire;

namespace TrustRent.Modules.Catalog.Services;

public class PropertyService : IPropertyService
{
    private readonly CatalogDbContext _context;
    private readonly IBackgroundJobClient _backgroundJobs;

    public PropertyService(CatalogDbContext context, IBackgroundJobClient backgroundJobs)
    {
        _context = context;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<Guid> CreatePropertyAsync(Guid landlordId, CreatePropertyDto dto, IEnumerable<FileDto> images, IList<string> imageCategories, IEnumerable<FileDto> documents)
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
            IsAvailable = false
        };

        var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp-uploads", property.Id.ToString());
        Directory.CreateDirectory(tempFolder);

        var savedFilePaths = new List<string>();

        foreach (var img in images)
        {
            if (img.Content.Length > 0)
            {
                var tempPath = Path.Combine(tempFolder, Guid.NewGuid() + Path.GetExtension(img.FileName));
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await img.Content.CopyToAsync(stream);
                }
                savedFilePaths.Add(tempPath);
            }
        }

        _backgroundJobs.Enqueue<Jobs.IPropertyUploadJob>(job =>
            job.ProcessCreationAsync(property.Id, landlordId, savedFilePaths, imageCategories.ToList())
        );

        /*
        // 2. Processar e fazer Upload das Imagens
        var newImagesList = images.ToList();
        for (int i = 0; i < newImagesList.Count; i++)
        {
            var img = newImagesList[i];

            // Garante que apanhamos a categoria certa, ou "Geral" se falhar
            var category = (imageCategories != null && i < imageCategories.Count)
                ? imageCategories[i]
                : "Geral";

            if (img.Content.Length > 0)
            {
                var imageUrl = await _imageService.UploadImageAsync(img.Content, img.FileName, $"properties/{property.Id}");
                property.Images.Add(new PropertyImage
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    Url = imageUrl,
                    Category = category, // <--- AGORA USA A CATEGORIA REAL
                    IsMain = false
                });
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
        */

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

    // Vai buscar o imóvel e traz as imagens agarradas
    public async Task<Property?> GetPropertyByIdAsync(Guid propertyId)
    {
        return await _context.Properties
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == propertyId);
    }

    // Atualiza os dados de texto e adiciona novas imagens se existirem
    public async Task UpdatePropertyAsync(
        Guid propertyId, 
        Guid landlordId, 
        CreatePropertyDto dto, 
        IEnumerable<FileDto> newImages, 
        IList<string> imageCategories,
        IList<Guid> retainedImageIds)
    {
        var property = await _context.Properties
            .Include(p => p.Images)
            .FirstOrDefaultAsync(p => p.Id == propertyId && p.LandlordId == landlordId);

        if (property == null) throw new Exception("Imóvel não encontrado ou não te pertence.");

        // Atualizar campos de texto
        property.Title = dto.Title;
        property.Description = dto.Description;
        property.Price = dto.Price;
        property.PropertyType = dto.PropertyType;
        property.Typology = dto.Typology;
        property.Area = dto.Area;
        property.Rooms = dto.Rooms;
        property.Bathrooms = dto.Bathrooms;
        property.Floor = dto.Floor;
        property.HasElevator = dto.HasElevator;
        property.HasAirConditioning = dto.HasAirConditioning;
        property.HasGarage = dto.HasGarage;
        property.AllowsPets = dto.AllowsPets;
        property.IsFurnished = dto.IsFurnished;
        property.FurnishedDescription = dto.FurnishedDescription;
        property.Street = dto.Street;
        property.PostalCode = dto.PostalCode;
        property.City = dto.City;
        property.Latitude = dto.Latitude;
        property.Longitude = dto.Longitude;

        var imagesToRemove = property.Images.Where(img => !retainedImageIds.Contains(img.Id)).ToList();
        var urlsToDeleteFromCloud = new List<string>();

        foreach (var img in imagesToRemove)
        {
            urlsToDeleteFromCloud.Add(img.Url);
            property.Images.Remove(img);
        }

        if (property.Images.Any() && !property.Images.Any(img => img.IsMain))
        {
            property.Images.First().IsMain = true;
        }

        var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp-uploads", property.Id.ToString());
        Directory.CreateDirectory(tempFolder);
        var savedFilePaths = new List<string>();

        var newImagesList = newImages.ToList();
        foreach (var img in newImagesList)
        {
            if (img.Content.Length > 0)
            {
                var tempPath = Path.Combine(tempFolder, Guid.NewGuid() + Path.GetExtension(img.FileName));
                using (var stream = new FileStream(tempPath, FileMode.Create))
                {
                    await img.Content.CopyToAsync(stream);
                }
                savedFilePaths.Add(tempPath);
            }
        }

        await _context.SaveChangesAsync();

        if (urlsToDeleteFromCloud.Any() || savedFilePaths.Any())
        {
            _backgroundJobs.Enqueue<Jobs.IPropertyUploadJob>(job =>
                job.ProcessEditAsync(propertyId, landlordId, savedFilePaths, imageCategories.ToList(), urlsToDeleteFromCloud)
            );
        }
    }
}