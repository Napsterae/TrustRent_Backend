using Hangfire;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.DTOs;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Mappers;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Services;

public class PropertyService : IPropertyService
{
    private readonly ICatalogUnitOfWork _uow;
    private readonly IBackgroundJobClient _backgroundJobs;

    public PropertyService(ICatalogUnitOfWork uow, IBackgroundJobClient backgroundJobs)
    {
        _uow = uow;
        _backgroundJobs = backgroundJobs;
    }

    public async Task<Guid> CreatePropertyAsync(
        Guid landlordId,
        CreatePropertyDto dto,
        IEnumerable<FileDto> images,
        IList<string> imageCategories,
        int mainImageIndex,
        IEnumerable<FileDto> documents,
        IList<Guid>? amenityIds = null,
        IList<int>? acceptedPeriodicities = null)
    {
        // 1. Mapear o DTO para o nosso Modelo da Base de Dados
        var property = dto.ToEntity(landlordId);

        // Adicionar comodidades selecionadas
        if (amenityIds != null)
        {
            foreach (var amenityId in amenityIds)
            {
                property.Amenities.Add(new PropertyAmenity { PropertyId = property.Id, AmenityId = amenityId });
            }
        }

        // Adicionar periodicidades selecionadas
        if (acceptedPeriodicities != null)
        {
            foreach (var months in acceptedPeriodicities)
            {
                property.AcceptedPeriodicities.Add(new PropertyPeriodicity
                {
                    Id = Guid.NewGuid(),
                    PropertyId = property.Id,
                    DurationMonths = months
                });
            }
        }

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

        property.IsUnderMaintenance = true;

        _backgroundJobs.Enqueue<Jobs.IPropertyUploadJob>(job =>
            job.ProcessCreationAsync(property.Id, landlordId, savedFilePaths, imageCategories.ToList(), mainImageIndex)
        );

        return property.Id; // Devolvemos o ID para o Frontend poder redirecionar o utilizador
    }

    public async Task<IEnumerable<PropertySummaryDto>> GetPropertiesByLandlordAsync(Guid landlordId)
    {
        var properties = await _uow.Properties.GetByLandlordIdWithImagesAsync(landlordId);

        return properties.ToSummaryDtoList();
    }

    // Vai buscar o imóvel e traz as imagens agarradas
    public async Task<Property?> GetPropertyByIdAsync(Guid propertyId)
    {
        return await _uow.Properties.GetByIdWithImagesAsync(propertyId);
    }

    // Atualiza os dados de texto e adiciona novas imagens se existirem
    public async Task UpdatePropertyAsync(
        Guid propertyId,
        Guid landlordId,
        CreatePropertyDto dto,
        IEnumerable<FileDto> newImages,
        IList<string> imageCategories,
        IList<Guid> retainedImageIds,
        int mainImageIndex,
        Guid? mainRetainedImageId,
        IList<Guid>? amenityIds = null,
        IList<int>? acceptedPeriodicities = null)
    {
        // 1. Carregar a entidade com todas as suas coleções
        var property = await _uow.Properties.GetByIdAndLandlordWithImagesAsync(propertyId, landlordId);

        if (property == null) throw new Exception("Imóvel não encontrado ou não te pertence.");

        // 2. Atualizar campos de texto (Usando o Mapper)
        dto.UpdateEntity(property);
        property.UpdatedAt = DateTime.UtcNow;

        // 3. Atualizar Comodidades (Amenities) de forma rastreada
        // Em vez de Clear(), removemos o que não está no DTO e adicionamos o novo
        var currentAmenityIds = property.Amenities.Select(a => a.AmenityId).ToList();
        var newAmenityIds = amenityIds ?? new List<Guid>();

        // Remover as que já não existem
        foreach (var existing in property.Amenities.Where(a => !newAmenityIds.Contains(a.AmenityId)).ToList())
        {
            property.Amenities.Remove(existing);
        }

        // Adicionar as novas
        foreach (var aid in newAmenityIds.Where(id => !currentAmenityIds.Contains(id)))
        {
            property.Amenities.Add(new PropertyAmenity { PropertyId = propertyId, AmenityId = aid });
        }

        // 4. Atualizar Periodicidades
        var currentPeriods = property.AcceptedPeriodicities.Select(p => p.DurationMonths).ToList();
        var newPeriods = acceptedPeriodicities ?? new List<int>();

        foreach (var existing in property.AcceptedPeriodicities.Where(p => !newPeriods.Contains(p.DurationMonths)).ToList())
        {
            property.AcceptedPeriodicities.Remove(existing);
        }

        foreach (var duration in newPeriods.Where(d => !currentPeriods.Contains(d)))
        {
            property.AcceptedPeriodicities.Add(new PropertyPeriodicity
            {
                PropertyId = propertyId,
                DurationMonths = duration
            });
        }

        // 5. Gestão de Imagens
        var imagesToRemove = property.Images.Where(img => !retainedImageIds.Contains(img.Id)).ToList();
        var urlsToDeleteFromCloud = new List<string>();

        foreach (var img in imagesToRemove)
        {
            urlsToDeleteFromCloud.Add(img.Url);
            property.Images.Remove(img);
        }

        // Resetar isMain das retidas
        foreach (var img in property.Images)
        {
            img.IsMain = false;
        }

        // Se a principal for uma das antigas
        if (mainRetainedImageId.HasValue && mainRetainedImageId.Value != Guid.Empty)
        {
            var mainImg = property.Images.FirstOrDefault(i => i.Id == mainRetainedImageId.Value);
            if (mainImg != null) mainImg.IsMain = true;
        }
        else if (mainImageIndex < 0 && property.Images.Any())
        {
            property.Images.First().IsMain = true;
        }

        // 6. Preparar novas imagens para processamento em background
        var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp-uploads", property.Id.ToString());
        if (!Directory.Exists(tempFolder)) Directory.CreateDirectory(tempFolder);
        
        var savedFilePaths = new List<string>();
        foreach (var img in newImages)
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

        bool requiresBackgroundProcessing = urlsToDeleteFromCloud.Any() || savedFilePaths.Any();
        if (requiresBackgroundProcessing)
        {
            property.IsUnderMaintenance = true;
        }

        // 7. Guardar Alterações
        try 
        {
            await _uow.SaveChangesAsync();
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Se chegamos aqui, o objeto foi modificado ou o ID é inválido para o EF
            throw new Exception("Erro de concorrência: Os dados do imóvel foram alterados por outro utilizador ou o registo já não existe.");
        }

        // 8. Enviar para Background Job se necessário
        if (requiresBackgroundProcessing)
        {
            _backgroundJobs.Enqueue<Jobs.IPropertyUploadJob>(job =>
                job.ProcessEditAsync(propertyId, landlordId, savedFilePaths, imageCategories.ToList(), urlsToDeleteFromCloud, mainImageIndex)
            );
        }
    }

    public async Task<PagedResult<PropertySearchDto>> SearchPropertiesAsync(PropertySearchQuery query)
    {
        var (items, totalCount) = await _uow.Properties.SearchAsync(query);

        return new PagedResult<PropertySearchDto>
        {
            Items = items.Select(p => p.ToSearchDto()),
            TotalCount = totalCount,
            Page = query.Page,
            PageSize = query.PageSize
        };
    }

    public async Task<IEnumerable<PropertySummaryDto>> GetPropertiesByTenantAsync(Guid tenantId)
    {
        var properties = await _uow.Properties.GetByTenantIdWithImagesAsync(tenantId);
        return properties.ToSummaryDtoList();
    }

    public async Task<IEnumerable<Amenity>> GetAllAmenitiesAsync()
    {
        return await _uow.Properties.GetAllAmenitiesAsync();
    }
}