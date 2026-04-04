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
        IEnumerable<FileDto> documents)
    {
        // 1. Mapear o DTO para o nosso Modelo da Base de Dados
        var property = dto.ToEntity(landlordId);

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
        Guid? mainRetainedImageId)
    {
        var property = await _uow.Properties.GetByIdAndLandlordWithImagesAsync(propertyId, landlordId);

        if (property == null) throw new Exception("Imóvel não encontrado ou não te pertence.");

        // Atualizar campos de texto
        dto.UpdateEntity(property);

        var imagesToRemove = property.Images.Where(img => !retainedImageIds.Contains(img.Id)).ToList();
        var urlsToDeleteFromCloud = new List<string>();

        foreach (var img in imagesToRemove)
        {
            urlsToDeleteFromCloud.Add(img.Url);
            property.Images.Remove(img);
        }

        // 1. Resetar TODAS as imagens antigas que ficaram (retained) para IsMain = false
        foreach (var img in property.Images)
        {
            img.IsMain = false;
        }

        // 2. Se a imagem principal for uma das antigas, ativamo-la já na Base de Dados
        if (mainRetainedImageId.HasValue && mainRetainedImageId.Value != Guid.Empty)
        {
            var mainImg = property.Images.FirstOrDefault(i => i.Id == mainRetainedImageId.Value);
            if (mainImg != null) mainImg.IsMain = true;
        }
        // Fallback de Segurança: Se não há nova imagem principal E não há imagem retida principal, assumimos a primeira das retidas
        else if (mainImageIndex < 0 && property.Images.Any())
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

        bool requiresBackgroundProcessing = urlsToDeleteFromCloud.Any() || savedFilePaths.Any();

        if (requiresBackgroundProcessing)
        {
            property.IsUnderMaintenance = true;
        }

        await _uow.SaveChangesAsync();

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
}