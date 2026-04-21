using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Models;

namespace TrustRent.Modules.Identity.Seeds;

/// <summary>
/// Seeds de dados de referência do módulo Identity.
/// Política: nunca apaga nem sobrescreve. Corre em todos os ambientes.
/// </summary>
public static class IdentityReferenceDataSeeder
{
    public static async Task SeedAsync(IdentityDbContext context)
    {
        await SeedPhoneCountriesAsync(context);
    }

    private static async Task SeedPhoneCountriesAsync(IdentityDbContext context)
    {
        var defaults = new[]
        {
            new PhoneCountry { IsoCode = "PT", Name = "Portugal", DialCode = "+351", FlagEmoji = "🇵🇹", MobilePattern = @"^9\d{8}$", Example = "912345678", DisplayOrder = 10, IsDefault = true },
            new PhoneCountry { IsoCode = "ES", Name = "Espanha", DialCode = "+34", FlagEmoji = "🇪🇸", MobilePattern = @"^[67]\d{8}$", Example = "612345678", DisplayOrder = 20 },
            new PhoneCountry { IsoCode = "FR", Name = "França", DialCode = "+33", FlagEmoji = "🇫🇷", MobilePattern = @"^[67]\d{8}$", Example = "612345678", DisplayOrder = 30 },
            new PhoneCountry { IsoCode = "DE", Name = "Alemanha", DialCode = "+49", FlagEmoji = "🇩🇪", MobilePattern = @"^1\d{9,11}$", Example = "15123456789", DisplayOrder = 40 },
            new PhoneCountry { IsoCode = "IT", Name = "Itália", DialCode = "+39", FlagEmoji = "🇮🇹", MobilePattern = @"^3\d{8,9}$", Example = "3123456789", DisplayOrder = 50 },
            new PhoneCountry { IsoCode = "GB", Name = "Reino Unido", DialCode = "+44", FlagEmoji = "🇬🇧", MobilePattern = @"^7\d{9}$", Example = "7400123456", DisplayOrder = 60 },
            new PhoneCountry { IsoCode = "IE", Name = "Irlanda", DialCode = "+353", FlagEmoji = "🇮🇪", MobilePattern = @"^8[35679]\d{7}$", Example = "831234567", DisplayOrder = 70 },
            new PhoneCountry { IsoCode = "NL", Name = "Países Baixos", DialCode = "+31", FlagEmoji = "🇳🇱", MobilePattern = @"^6\d{8}$", Example = "612345678", DisplayOrder = 80 },
            new PhoneCountry { IsoCode = "BE", Name = "Bélgica", DialCode = "+32", FlagEmoji = "🇧🇪", MobilePattern = @"^4\d{8}$", Example = "470123456", DisplayOrder = 90 },
            new PhoneCountry { IsoCode = "LU", Name = "Luxemburgo", DialCode = "+352", FlagEmoji = "🇱🇺", MobilePattern = @"^(621|628|661|671|691)\d{6}$", Example = "621123456", DisplayOrder = 100 },
            new PhoneCountry { IsoCode = "CH", Name = "Suíça", DialCode = "+41", FlagEmoji = "🇨🇭", MobilePattern = @"^7[5-9]\d{7}$", Example = "791234567", DisplayOrder = 110 },
            new PhoneCountry { IsoCode = "US", Name = "Estados Unidos", DialCode = "+1", FlagEmoji = "🇺🇸", MobilePattern = @"^[2-9]\d{9}$", Example = "2025550123", DisplayOrder = 120 },
            new PhoneCountry { IsoCode = "CA", Name = "Canadá", DialCode = "+1", FlagEmoji = "🇨🇦", MobilePattern = @"^[2-9]\d{9}$", Example = "4165550123", DisplayOrder = 130 },
            new PhoneCountry { IsoCode = "BR", Name = "Brasil", DialCode = "+55", FlagEmoji = "🇧🇷", MobilePattern = @"^\d{2}9\d{8}$", Example = "11912345678", DisplayOrder = 140 },
            new PhoneCountry { IsoCode = "AO", Name = "Angola", DialCode = "+244", FlagEmoji = "🇦🇴", MobilePattern = @"^9[1-5]\d{7}$", Example = "923456789", DisplayOrder = 150 },
            new PhoneCountry { IsoCode = "MZ", Name = "Moçambique", DialCode = "+258", FlagEmoji = "🇲🇿", MobilePattern = @"^8[2-7]\d{7}$", Example = "841234567", DisplayOrder = 160 },
            new PhoneCountry { IsoCode = "CV", Name = "Cabo Verde", DialCode = "+238", FlagEmoji = "🇨🇻", MobilePattern = @"^[59]\d{6}$", Example = "9912345", DisplayOrder = 170 },
            new PhoneCountry { IsoCode = "ST", Name = "São Tomé e Príncipe", DialCode = "+239", FlagEmoji = "🇸🇹", MobilePattern = @"^9\d{6}$", Example = "9812345", DisplayOrder = 180 },
            new PhoneCountry { IsoCode = "GW", Name = "Guiné-Bissau", DialCode = "+245", FlagEmoji = "🇬🇼", MobilePattern = @"^[5-7]\d{6}$", Example = "6512345", DisplayOrder = 190 },
            new PhoneCountry { IsoCode = "TL", Name = "Timor-Leste", DialCode = "+670", FlagEmoji = "🇹🇱", MobilePattern = @"^7\d{7}$", Example = "77234567", DisplayOrder = 200 }
        };

        var existingCodes = await context.PhoneCountries.Select(c => c.IsoCode).ToListAsync();
        var toAdd = defaults
            .Where(d => !existingCodes.Contains(d.IsoCode))
            .Select(d => { d.Id = Guid.NewGuid(); return d; })
            .ToList();

        if (toAdd.Count > 0)
        {
            await context.PhoneCountries.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] Identity ReferenceData: Adicionados {toAdd.Count} países de telefone.");
        }
    }
}
