using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Contracts.DTOs;
using TrustRent.Modules.Admin.Contracts.Interfaces;
using TrustRent.Modules.Admin.Models;

namespace TrustRent.Modules.Admin.Services;

public class StagingAccessService : IStagingAccessService
{
    private const string UsersSettingKey = "staging.access.users";
    private const string SimulationsSettingKey = "staging.simulations.enabled";
    private static readonly Regex UsernameRegex = new("^[a-zA-Z0-9._-]{3,64}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly AdminDbContext _db;

    public StagingAccessService(AdminDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<StagingAccessUserDto>> ListUsersAsync(CancellationToken ct = default)
    {
        var users = await LoadUsersAsync(ct);
        return users
            .OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase)
            .Select(ToDto)
            .ToList();
    }

    public async Task<int> CountActiveUsersAsync(CancellationToken ct = default)
    {
        var users = await LoadUsersAsync(ct);
        return users.Count(x => x.IsActive);
    }

    public async Task<StagingAccessUserDto?> GetUserAsync(string username, CancellationToken ct = default)
    {
        var users = await LoadUsersAsync(ct);
        var user = FindUser(users, username);
        return user is null ? null : ToDto(user);
    }

    public async Task<StagingAccessUserDto> CreateUserAsync(CreateStagingAccessUserRequest request, Guid? updatedByAdminId = null, CancellationToken ct = default)
    {
        var username = NormalizeUsername(request.Username);
        var displayName = NormalizeDisplayName(request.DisplayName, username);
        ValidatePassword(request.Password);

        var users = await LoadUsersAsync(ct);
        if (FindUser(users, username) is not null)
        {
            throw new InvalidOperationException("Já existe um utilizador de acesso staging com esse username.");
        }

        var now = DateTime.UtcNow;
        var created = new StoredStagingAccessUser(
            username,
            displayName,
            BCrypt.Net.BCrypt.HashPassword(request.Password),
            request.IsActive,
            now,
            now);

        users.Add(created);
        await SaveUsersAsync(users, updatedByAdminId, ct);
        return ToDto(created);
    }

    public async Task<StagingAccessUserDto> UpdateUserAsync(string username, UpdateStagingAccessUserRequest request, Guid? updatedByAdminId = null, CancellationToken ct = default)
    {
        var users = await LoadUsersAsync(ct);
        var existing = FindUser(users, username) ?? throw new InvalidOperationException("Utilizador de acesso staging não encontrado.");
        var updated = existing with
        {
            DisplayName = NormalizeDisplayName(request.DisplayName, existing.Username),
            IsActive = request.IsActive,
            UpdatedAt = DateTime.UtcNow,
        };

        ReplaceUser(users, existing, updated);
        await SaveUsersAsync(users, updatedByAdminId, ct);
        return ToDto(updated);
    }

    public async Task ResetPasswordAsync(string username, string newPassword, Guid? updatedByAdminId = null, CancellationToken ct = default)
    {
        ValidatePassword(newPassword);

        var users = await LoadUsersAsync(ct);
        var existing = FindUser(users, username) ?? throw new InvalidOperationException("Utilizador de acesso staging não encontrado.");
        var updated = existing with
        {
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword),
            UpdatedAt = DateTime.UtcNow,
        };

        ReplaceUser(users, existing, updated);
        await SaveUsersAsync(users, updatedByAdminId, ct);
    }

    public async Task DeleteUserAsync(string username, Guid? updatedByAdminId = null, CancellationToken ct = default)
    {
        var users = await LoadUsersAsync(ct);
        var existing = FindUser(users, username) ?? throw new InvalidOperationException("Utilizador de acesso staging não encontrado.");
        users.Remove(existing);
        await SaveUsersAsync(users, updatedByAdminId, ct);
    }

    public async Task<StagingAccessUserSessionDto?> GetActiveUserAsync(string username, CancellationToken ct = default)
    {
        var users = await LoadUsersAsync(ct);
        var user = FindUser(users, username);
        return user is { IsActive: true }
            ? new StagingAccessUserSessionDto(user.Username, user.DisplayName)
            : null;
    }

    public async Task<StagingAccessUserSessionDto?> ValidateCredentialsAsync(string username, string password, CancellationToken ct = default)
    {
        var users = await LoadUsersAsync(ct);
        var user = FindUser(users, username);
        if (user is not { IsActive: true }) return null;
        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash)) return null;
        return new StagingAccessUserSessionDto(user.Username, user.DisplayName);
    }

    public async Task<bool?> GetSimulationOverrideAsync(CancellationToken ct = default)
    {
        var raw = await _db.PlatformSettings
            .AsNoTracking()
            .Where(x => x.Key == SimulationsSettingKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (bool.TryParse(raw, out var enabled)) return enabled;
        throw new InvalidOperationException("A configuração de simulações de staging está inválida.");
    }

    public async Task<bool> SetSimulationOverrideAsync(bool enabled, Guid? updatedByAdminId = null, CancellationToken ct = default)
    {
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == SimulationsSettingKey, ct);
        if (setting is null)
        {
            setting = new PlatformSetting { Key = SimulationsSettingKey };
            _db.PlatformSettings.Add(setting);
        }

        setting.Value = enabled.ToString();
        setting.Category = "staging";
        setting.Description = "Permite simulações de documentos e validações em staging.";
        setting.ValueType = "bool";
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByAdminId = updatedByAdminId;
        await _db.SaveChangesAsync(ct);
        return enabled;
    }

    private async Task<List<StoredStagingAccessUser>> LoadUsersAsync(CancellationToken ct)
    {
        var raw = await _db.PlatformSettings
            .AsNoTracking()
            .Where(x => x.Key == UsersSettingKey)
            .Select(x => x.Value)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(raw)) return [];

        try
        {
            return JsonSerializer.Deserialize<List<StoredStagingAccessUser>>(raw, JsonOptions) ?? [];
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException("A configuração de utilizadores staging está inválida.", ex);
        }
    }

    private async Task SaveUsersAsync(List<StoredStagingAccessUser> users, Guid? updatedByAdminId, CancellationToken ct)
    {
        var setting = await _db.PlatformSettings.FirstOrDefaultAsync(x => x.Key == UsersSettingKey, ct);
        if (setting is null)
        {
            setting = new PlatformSetting { Key = UsersSettingKey };
            _db.PlatformSettings.Add(setting);
        }

        setting.Value = JsonSerializer.Serialize(
            users.OrderBy(x => x.Username, StringComparer.OrdinalIgnoreCase),
            JsonOptions);
        setting.Category = "staging";
        setting.Description = "Utilizadores autorizados a entrar no frontend staging.";
        setting.ValueType = "json";
        setting.UpdatedAt = DateTime.UtcNow;
        setting.UpdatedByAdminId = updatedByAdminId;
        await _db.SaveChangesAsync(ct);
    }

    private static string NormalizeUsername(string username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            throw new ArgumentException("Username obrigatório.");
        }

        var normalized = username.Trim();
        if (!UsernameRegex.IsMatch(normalized))
        {
            throw new ArgumentException("Username deve ter 3 a 64 caracteres e só pode conter letras, números, ponto, hífen ou underscore.");
        }

        return normalized;
    }

    private static string NormalizeDisplayName(string? displayName, string fallbackUsername)
    {
        var normalized = string.IsNullOrWhiteSpace(displayName) ? fallbackUsername : displayName.Trim();
        if (normalized.Length > 120)
        {
            throw new ArgumentException("Display name demasiado longo.");
        }

        return normalized;
    }

    private static void ValidatePassword(string password)
    {
        if (string.IsNullOrWhiteSpace(password) || password.Length < 12)
        {
            throw new ArgumentException("Password deve ter pelo menos 12 caracteres.");
        }
    }

    private static StoredStagingAccessUser? FindUser(IEnumerable<StoredStagingAccessUser> users, string username)
    {
        return users.FirstOrDefault(x => string.Equals(x.Username, username?.Trim(), StringComparison.OrdinalIgnoreCase));
    }

    private static void ReplaceUser(List<StoredStagingAccessUser> users, StoredStagingAccessUser existing, StoredStagingAccessUser updated)
    {
        var index = users.FindIndex(x => string.Equals(x.Username, existing.Username, StringComparison.OrdinalIgnoreCase));
        if (index < 0)
        {
            throw new InvalidOperationException("Utilizador de acesso staging não encontrado.");
        }

        users[index] = updated;
    }

    private static StagingAccessUserDto ToDto(StoredStagingAccessUser user)
    {
        return new StagingAccessUserDto(user.Username, user.DisplayName, user.IsActive, user.CreatedAt, user.UpdatedAt);
    }

    private sealed record StoredStagingAccessUser(
        string Username,
        string DisplayName,
        string PasswordHash,
        bool IsActive,
        DateTime CreatedAt,
        DateTime UpdatedAt
    );
}