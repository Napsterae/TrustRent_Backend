using System.Globalization;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Catalog.Contracts.Database;
using TrustRent.Modules.Catalog.Models.ReferenceData;

namespace TrustRent.Modules.Catalog.Seeds;

/// <summary>
/// Seeds de dados de referência (editáveis via back-office).
/// Política: nunca apaga nem sobrescreve registos existentes. Só adiciona os que faltam.
/// Corre em TODOS os ambientes (não só Development) — ao contrário de CatalogSeeder que só mete demo data.
/// </summary>
public static class ReferenceDataSeeder
{
    public static async Task SeedAsync(CatalogDbContext context)
    {
        await SeedPropertyTypesAsync(context);
        await SeedTypologiesAsync(context);
        await SeedLocationsAsync(context);
        await SeedSalaryRangesAsync(context);
    }

    // ===== Property Types =====
    private static async Task SeedPropertyTypesAsync(CatalogDbContext context)
    {
        var defaults = new[]
        {
            new PropertyType { Code = "APARTMENT", Name = "Apartamento", DisplayOrder = 10 },
            new PropertyType { Code = "HOUSE", Name = "Moradia", DisplayOrder = 20 },
            new PropertyType { Code = "ROOM", Name = "Quarto", DisplayOrder = 30 }
        };

        var existingCodes = await context.PropertyTypes.Select(t => t.Code).ToListAsync();
        var toAdd = defaults
            .Where(d => !existingCodes.Contains(d.Code))
            .Select(d => { d.Id = Guid.NewGuid(); return d; })
            .ToList();

        if (toAdd.Count > 0)
        {
            await context.PropertyTypes.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] ReferenceData: Adicionados {toAdd.Count} tipos de imóvel.");
        }
    }

    // ===== Typologies =====
    private static async Task SeedTypologiesAsync(CatalogDbContext context)
    {
        var defaults = new[]
        {
            new Typology { Code = "STUDIO", Name = "Studio", Bedrooms = 0, DisplayOrder = 5 },
            new Typology { Code = "T0", Name = "T0", Bedrooms = 0, DisplayOrder = 10 },
            new Typology { Code = "T1", Name = "T1", Bedrooms = 1, DisplayOrder = 20 },
            new Typology { Code = "T2", Name = "T2", Bedrooms = 2, DisplayOrder = 30 },
            new Typology { Code = "T3", Name = "T3", Bedrooms = 3, DisplayOrder = 40 },
            new Typology { Code = "T4", Name = "T4", Bedrooms = 4, DisplayOrder = 50 },
            new Typology { Code = "T5", Name = "T5", Bedrooms = 5, DisplayOrder = 60 },
            new Typology { Code = "T6+", Name = "T6 ou superior", Bedrooms = 6, DisplayOrder = 70 }
        };

        var existingCodes = await context.Typologies.Select(t => t.Code).ToListAsync();
        var toAdd = defaults
            .Where(d => !existingCodes.Contains(d.Code))
            .Select(d => { d.Id = Guid.NewGuid(); return d; })
            .ToList();

        if (toAdd.Count > 0)
        {
            await context.Typologies.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] ReferenceData: Adicionadas {toAdd.Count} tipologias.");
        }
    }

    // ===== Salary Ranges =====
    private static async Task SeedSalaryRangesAsync(CatalogDbContext context)
    {
        var defaults = new[]
        {
            new SalaryRange { Code = "LT_1000",     Label = "< 1.000 €",            MinAmount = null,    MaxAmount = 1000m, DisplayOrder = 10 },
            new SalaryRange { Code = "R_1000_2000", Label = "1.000 € – 2.000 €",    MinAmount = 1000m,   MaxAmount = 2000m, DisplayOrder = 20 },
            new SalaryRange { Code = "R_2000_3000", Label = "2.000 € – 3.000 €",    MinAmount = 2000m,   MaxAmount = 3000m, DisplayOrder = 30 },
            new SalaryRange { Code = "R_3000_5000", Label = "3.000 € – 5.000 €",    MinAmount = 3000m,   MaxAmount = 5000m, DisplayOrder = 40 },
            new SalaryRange { Code = "GT_5000",     Label = "> 5.000 €",            MinAmount = 5000m,   MaxAmount = null,  DisplayOrder = 50 }
        };

        var existingCodes = await context.SalaryRanges.Select(r => r.Code).ToListAsync();
        var toAdd = defaults
            .Where(d => !existingCodes.Contains(d.Code))
            .Select(d => { d.Id = Guid.NewGuid(); return d; })
            .ToList();

        if (toAdd.Count > 0)
        {
            await context.SalaryRanges.AddRangeAsync(toAdd);
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] ReferenceData: Adicionadas {toAdd.Count} faixas salariais.");
        }
    }

    // ===== Locations (Districts / Municipalities / Parishes) =====
    private static async Task SeedLocationsAsync(CatalogDbContext context)
    {
        var json = ReadEmbeddedResource("TrustRent.Modules.Catalog.Seeds.Data.locations.json");
        if (string.IsNullOrWhiteSpace(json))
        {
            Console.WriteLine("[SEED] ReferenceData: locations.json não encontrado. A ignorar.");
            return;
        }

        var payload = JsonSerializer.Deserialize<List<DistrictDto>>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        if (payload == null) return;

        // Pré-carregar estado actual para evitar N+1
        var existingDistricts = await context.Districts.ToListAsync();
        var existingMunicipalities = await context.Municipalities.ToListAsync();
        var existingParishes = await context.Parishes.ToListAsync();

        int addedDistricts = 0, addedMunicipalities = 0, addedParishes = 0;
        int districtOrder = 0;

        foreach (var distDto in payload)
        {
            districtOrder += 10;
            var distCode = Slugify(distDto.District);

            var district = existingDistricts.FirstOrDefault(d => d.Code == distCode);
            if (district == null)
            {
                district = new District
                {
                    Id = Guid.NewGuid(),
                    Code = distCode,
                    Name = distDto.District,
                    DisplayOrder = districtOrder,
                    IsActive = true,
                    IsSystemDefault = true
                };
                context.Districts.Add(district);
                existingDistricts.Add(district);
                addedDistricts++;
            }

            int munOrder = 0;
            foreach (var munDto in distDto.Municipalities ?? new())
            {
                munOrder += 10;
                var munCode = Slugify(munDto.Name);
                var municipality = existingMunicipalities.FirstOrDefault(m => m.DistrictId == district.Id && m.Code == munCode);
                if (municipality == null)
                {
                    municipality = new Municipality
                    {
                        Id = Guid.NewGuid(),
                        DistrictId = district.Id,
                        Code = munCode,
                        Name = munDto.Name,
                        DisplayOrder = munOrder,
                        IsActive = true,
                        IsSystemDefault = true
                    };
                    context.Municipalities.Add(municipality);
                    existingMunicipalities.Add(municipality);
                    addedMunicipalities++;
                }

                int parishOrder = 0;
                foreach (var parishName in munDto.Parishes ?? new())
                {
                    parishOrder += 10;
                    var parishCode = Slugify(parishName);
                    if (existingParishes.Any(p => p.MunicipalityId == municipality.Id && p.Code == parishCode))
                        continue;

                    var parish = new Parish
                    {
                        Id = Guid.NewGuid(),
                        MunicipalityId = municipality.Id,
                        Code = parishCode,
                        Name = parishName,
                        DisplayOrder = parishOrder,
                        IsActive = true,
                        IsSystemDefault = true
                    };
                    context.Parishes.Add(parish);
                    existingParishes.Add(parish);
                    addedParishes++;
                }
            }
        }

        if (addedDistricts + addedMunicipalities + addedParishes > 0)
        {
            await context.SaveChangesAsync();
            Console.WriteLine($"[SEED] ReferenceData: Adicionados {addedDistricts} distritos, {addedMunicipalities} concelhos, {addedParishes} freguesias.");
        }
        else
        {
            Console.WriteLine("[SEED] ReferenceData: Localizações já actualizadas.");
        }
    }

    // ===== Helpers =====
    private static string ReadEmbeddedResource(string resourceName)
    {
        var asm = Assembly.GetExecutingAssembly();
        using var stream = asm.GetManifestResourceStream(resourceName);
        if (stream == null) return string.Empty;
        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static string Slugify(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;

        // Remover diacríticos
        var normalized = input.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(normalized.Length);
        foreach (var c in normalized)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        var ascii = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

        var result = new StringBuilder(ascii.Length);
        foreach (var c in ascii)
        {
            if (c >= 'a' && c <= 'z' || c >= '0' && c <= '9') result.Append(c);
            else if (c == ' ' || c == '-' || c == '_' || c == '/' || c == ',' || c == '.') result.Append('-');
        }
        var slug = result.ToString();
        while (slug.Contains("--")) slug = slug.Replace("--", "-");
        return slug.Trim('-');
    }

    private sealed class DistrictDto
    {
        public string District { get; set; } = string.Empty;
        public List<MunicipalityDto>? Municipalities { get; set; }
    }

    private sealed class MunicipalityDto
    {
        public string Name { get; set; } = string.Empty;
        public List<string>? Parishes { get; set; }
    }
}
