using System.Xml.Linq;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Api.Services;

public sealed class PlatformSettingsXmlRepository : IXmlRepository
{
    private const string KeyPrefix = "security:data-protection:key:";

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PlatformSettingsXmlRepository> _logger;

    public PlatformSettingsXmlRepository(
        IServiceScopeFactory scopeFactory,
        ILogger<PlatformSettingsXmlRepository> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public IReadOnlyCollection<XElement> GetAllElements()
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        var settings = db.PlatformSettings
            .AsNoTracking()
            .Where(setting => setting.Key.StartsWith(KeyPrefix))
            .OrderBy(setting => setting.Key)
            .ToList();

        var elements = new List<XElement>(settings.Count);
        foreach (var setting in settings)
        {
            if (string.IsNullOrWhiteSpace(setting.Value))
                continue;

            try
            {
                elements.Add(XElement.Parse(setting.Value));
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Ignorar chave Data Protection invalida guardada em {SettingKey}.", setting.Key);
            }
        }

        return elements;
    }

    public void StoreElement(XElement element, string friendlyName)
    {
        ArgumentNullException.ThrowIfNull(element);

        var normalizedFriendlyName = string.IsNullOrWhiteSpace(friendlyName)
            ? Guid.NewGuid().ToString("N")
            : friendlyName.Trim();
        var settingKey = KeyPrefix + normalizedFriendlyName;
        var serializedElement = element.ToString(SaveOptions.DisableFormatting);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AdminDbContext>();

        var setting = db.PlatformSettings.FirstOrDefault(existing => existing.Key == settingKey);
        if (setting is null)
        {
            setting = new PlatformSetting
            {
                Key = settingKey,
                Category = "security",
                Description = "ASP.NET Core Data Protection key ring entry.",
                ValueType = "xml"
            };
            db.PlatformSettings.Add(setting);
        }

        setting.Value = serializedElement;
        setting.UpdatedAt = DateTime.UtcNow;
        db.SaveChanges();
    }
}