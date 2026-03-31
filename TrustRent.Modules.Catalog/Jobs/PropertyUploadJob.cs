using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Contracts.Interfaces;
using TrustRent.Modules.Catalog.Models;
using TrustRent.Shared.Contracts.Interfaces;

namespace TrustRent.Modules.Catalog.Jobs;

public interface IPropertyUploadJob
{
    Task ProcessCreationAsync(Guid propertyId, Guid landlordId, List<string> tempFilePaths, List<string> categories, int mainImageIndex);
    Task ProcessEditAsync(Guid propertyId, Guid landlordId, List<string> tempFilePaths, List<string> categories, List<string> imageUrlsToDelete, int mainImageIndex);
}

public class PropertyUploadJob : IPropertyUploadJob
{
    private readonly ICatalogUnitOfWork _uow;
    private readonly IImageService _imageService;
    private readonly INotificationService _notificationService;

    public PropertyUploadJob(ICatalogUnitOfWork uow, IImageService imageService, INotificationService notificationService)
    {
        _uow = uow;
        _imageService = imageService;
        _notificationService = notificationService;
    }

    public async Task ProcessCreationAsync(Guid propertyId, Guid landlordId, List<string> tempFilePaths, List<string> categories, int mainImageIndex)
    {
        var property = await _uow.Properties.GetByIdAsync(propertyId);
        if (property == null) return;

        try
        {
            // 1. Processar cada imagem pendente
            for (int i = 0; i < tempFilePaths.Count; i++)
            {
                var filePath = tempFilePaths[i];
                var category = (categories != null && i < categories.Count) ? categories[i] : "Geral";

                if (File.Exists(filePath))
                {
                    using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                    {
                        // Upload pesado para a Cloud (demora o tempo que for preciso sem bloquear ninguém)
                        var imageUrl = await _imageService.UploadImageAsync(stream, Path.GetFileName(filePath), $"properties/{propertyId}");

                        property.Images.Add(new PropertyImage
                        {
                            Id = Guid.NewGuid(),
                            PropertyId = propertyId,
                            Url = imageUrl,
                            Category = category,
                            IsMain = (i == mainImageIndex)
                        });
                    }

                    // Limpar o ficheiro temporário do disco do nosso servidor após upload
                    File.Delete(filePath);
                }
            }

            await _uow.SaveChangesAsync();

            // 3. Notificar o Senhorio que o anúncio já está online!
            await _notificationService.SendNotificationAsync(
                landlordId,
                "application", // Ícone
                $"As imagens do teu anúncio '{property.Title}' foram processadas e o imóvel já está público!"
            );
        }
        catch (Exception ex)
        {
            // O Hangfire tenta novamente de forma automática caso haja falhas de rede com a Cloud!
            Console.WriteLine($"Erro no Job de Upload: {ex.Message}");
            throw;
        }
        finally
        {
            var exProperty = await _uow.Properties.GetByIdAsync(propertyId);
            if (exProperty != null)
            {
                exProperty.IsUnderMaintenance = false;
                await _uow.SaveChangesAsync();
            }

            // Limpar a pasta temporária do imóvel (caso tenha ficado vazia)
            var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp-uploads", propertyId.ToString());
            if (Directory.Exists(tempFolder) && !Directory.EnumerateFileSystemEntries(tempFolder).Any())
            {
                Directory.Delete(tempFolder);
            }
        }
    }

    public async Task ProcessEditAsync(Guid propertyId, Guid landlordId, List<string> tempFilePaths, List<string> categories, List<string> imageUrlsToDelete, int mainImageIndex)
    {
        // 1. APAGAR AS IMAGENS VELHAS DA CLOUD (Em background)
        foreach (var urlToDelete in imageUrlsToDelete)
        {
            try
            {
                await _imageService.DeleteImageAsync(urlToDelete);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro ao apagar imagem órfã {urlToDelete}: {ex.Message}");
                // Não atiramos throw aqui para não interromper os uploads novos se falhar a apagar uma velha
            }
        }

        // 2. FAZER UPLOAD DAS NOVAS IMAGENS (Se existirem)
        if (tempFilePaths.Any())
        {
            var property = await _uow.Properties.GetByIdAsync(propertyId);
            if (property == null) return;

            try
            {
                bool hasMainImage = property.Images.Any(img => img.IsMain);

                for (int i = 0; i < tempFilePaths.Count; i++)
                {
                    var filePath = tempFilePaths[i];
                    var category = (categories != null && i < categories.Count) ? categories[i] : "Geral";

                    if (File.Exists(filePath))
                    {
                        using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                        {
                            var imageUrl = await _imageService.UploadImageAsync(stream, Path.GetFileName(filePath), $"properties/{propertyId}");

                            bool isThisMain = (i == mainImageIndex);

                            await _uow.Properties.AddImageAsync(new PropertyImage
                            {
                                Id = Guid.NewGuid(),
                                PropertyId = propertyId,
                                Url = imageUrl,
                                Category = category,
                                IsMain = isThisMain
                            });
                        }
                        File.Delete(filePath);
                    }
                }

                await _uow.SaveChangesAsync();

                // Notificar que a atualização das fotos terminou
                await _notificationService.SendNotificationAsync(
                    landlordId,
                    "application",
                    $"A galeria do teu imóvel '{property.Title}' acabou de ser atualizada!"
                );
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Erro no Job de Edição: {ex.Message}");
                throw;
            }
            finally
            {
                var exProperty = await _uow.Properties.GetByIdAsync(propertyId);
                if (exProperty != null)
                {
                    exProperty.IsUnderMaintenance = false;
                    await _uow.SaveChangesAsync();
                }

                var tempFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "temp-uploads", propertyId.ToString());
                if (Directory.Exists(tempFolder) && !Directory.EnumerateFileSystemEntries(tempFolder).Any())
                {
                    Directory.Delete(tempFolder);
                }
            }
        }
    }
}