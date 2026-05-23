using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using TrustRent.Modules.Admin.Contracts.Database;
using TrustRent.Modules.Admin.Models;
using TrustRent.Modules.Identity.Contracts.Database;
using TrustRent.Modules.Identity.Contracts.Interfaces;
using TrustRent.Modules.Identity.Models;

namespace TrustRent.Api.Services;

public sealed class TelegramMessagingPlatformService : ITelegramMessagingPlatformService
{
    private const int VerificationTtlMinutes = 15;
    private const string BotTokenSettingKey = "telegram.bot_token";
    private const string BotUsernameSettingKey = "telegram.bot_username";
    private const string LastUpdateIdSettingKey = "telegram.last_update_id";

    private readonly HttpClient _httpClient;
    private readonly AdminDbContext _adminDb;
    private readonly IdentityDbContext _identityDb;
    private readonly ILogger<TelegramMessagingPlatformService> _logger;

    public TelegramMessagingPlatformService(
        HttpClient httpClient,
        AdminDbContext adminDb,
        IdentityDbContext identityDb,
        ILogger<TelegramMessagingPlatformService> logger)
    {
        _httpClient = httpClient;
        _adminDb = adminDb;
        _identityDb = identityDb;
        _logger = logger;
    }

    public async Task<TelegramPhoneVerificationStartResult> StartPhoneVerificationAsync(Guid userId, string phoneNumber, CancellationToken ct = default)
    {
        var settings = await GetConfiguredSettingsAsync(ct);
        await SyncUpdatesAsync(settings, ct);

        var user = await _identityDb.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");

        var now = DateTime.UtcNow;
        var normalizedPhoneNumber = NormalizePhoneNumber(phoneNumber);
        user.TelegramPendingVerificationToken = Guid.NewGuid().ToString("N");
        user.TelegramPendingExpectedPhoneNumber = normalizedPhoneNumber;
        user.TelegramPendingVerificationExpiresAt = now.AddMinutes(VerificationTtlMinutes);
        user.TelegramPendingVerificationError = null;

        await _identityDb.SaveChangesAsync(ct);

        if (!string.IsNullOrWhiteSpace(user.TelegramChatId))
        {
            await SendContactRequestAsync(settings.BotToken, user.TelegramChatId, user.PendingPhoneNumber ?? normalizedPhoneNumber, ct);
            return new TelegramPhoneVerificationStartResult(
                "Enviámos instruções no Telegram. Abre a conversa com o bot e partilha o teu contacto para concluir a validação.",
                user.TelegramPendingVerificationExpiresAt!.Value,
                null,
                settings.BotUsername,
                true);
        }

        return new TelegramPhoneVerificationStartResult(
            "Abre o bot no Telegram, inicia a conversa e partilha o teu contacto para validar este número.",
            user.TelegramPendingVerificationExpiresAt!.Value,
            BuildDeepLink(settings.BotUsername, user.TelegramPendingVerificationToken!),
            settings.BotUsername,
            false);
    }

    public async Task<TelegramPhoneVerificationStatusResult> GetPhoneVerificationStatusAsync(Guid userId, CancellationToken ct = default)
    {
        var settings = await TryGetSettingsAsync(ct);
        var user = await _identityDb.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");
        var isCurrentPhoneVerified = !HasPendingPhone(user) && user.IsPhoneNumberVerified;

        if (settings == null)
        {
            return new TelegramPhoneVerificationStatusResult(
                false,
                !string.IsNullOrWhiteSpace(user.TelegramChatId),
                false,
                isCurrentPhoneVerified,
                "O Telegram da plataforma ainda não está configurado no backoffice.",
                null,
                null,
                user.TelegramUsername);
        }

        await SyncUpdatesAsync(settings, ct);
        user = await _identityDb.Users.SingleOrDefaultAsync(x => x.Id == userId, ct)
            ?? throw new InvalidOperationException("Utilizador não encontrado.");

        var now = DateTime.UtcNow;
        var hasPendingVerification = user.TelegramPendingVerificationExpiresAt.HasValue
            && user.TelegramPendingVerificationExpiresAt > now
            && !string.IsNullOrWhiteSpace(user.TelegramPendingExpectedPhoneNumber);
        var hasStartedConversation = !string.IsNullOrWhiteSpace(user.TelegramChatId);
        isCurrentPhoneVerified = !HasPendingPhone(user) && user.IsPhoneNumberVerified;
        var awaitingContactShare = hasPendingVerification && hasStartedConversation && !isCurrentPhoneVerified;
        var deepLinkUrl = hasPendingVerification && !hasStartedConversation && !string.IsNullOrWhiteSpace(user.TelegramPendingVerificationToken)
            ? BuildDeepLink(settings.BotUsername, user.TelegramPendingVerificationToken)
            : null;

        var message = isCurrentPhoneVerified
            ? "Número validado com sucesso através do Telegram."
            : !string.IsNullOrWhiteSpace(user.TelegramPendingVerificationError)
                ? user.TelegramPendingVerificationError
                : awaitingContactShare
                    ? "Partilha o teu contacto no bot Telegram para concluir a validação."
                    : deepLinkUrl is not null
                        ? "Abre o bot com o link de validação e partilha o teu contacto. Se já tens a conversa aberta, partilha o contacto no bot para concluir."
                        : hasStartedConversation
                            ? "A conversa com o bot já está ligada. Pede uma nova validação para este número quando precisares."
                            : "Seleciona Telegram e pede a validação do número para começar.";

        return new TelegramPhoneVerificationStatusResult(
            true,
            hasStartedConversation,
            awaitingContactShare,
            isCurrentPhoneVerified,
            message,
            deepLinkUrl,
            user.TelegramPendingVerificationExpiresAt,
            user.TelegramUsername);
    }

    public async Task SendLoginCodeAsync(User user, string code, CancellationToken ct = default)
    {
        var settings = await GetConfiguredSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(user.TelegramChatId))
            throw new InvalidOperationException("O utilizador ainda não ligou o Telegram à conta.");

        await SendTextMessageAsync(
            settings.BotToken,
            user.TelegramChatId,
            $"Codigo de acesso Wekaza: {code}\n\nSe nao foste tu a pedir este codigo, ignora esta mensagem.",
            ct);
    }

    public async Task SendNotificationAsync(User user, string message, CancellationToken ct = default)
    {
        var settings = await GetConfiguredSettingsAsync(ct);
        if (string.IsNullOrWhiteSpace(user.TelegramChatId))
            throw new InvalidOperationException("O utilizador ainda não ligou o Telegram à conta.");

        await SendTextMessageAsync(settings.BotToken, user.TelegramChatId, message, ct);
    }

    private async Task SyncUpdatesAsync(TelegramSettings settings, CancellationToken ct)
    {
        TelegramUpdatesResponse? response;
        try
        {
            var offset = settings.LastUpdateId.HasValue ? settings.LastUpdateId.Value + 1 : (long?)null;
            var path = offset.HasValue
                ? $"/bot{settings.BotToken}/getUpdates?offset={offset.Value}&limit=100&timeout=0"
                : $"/bot{settings.BotToken}/getUpdates?limit=100&timeout=0";

            response = await _httpClient.GetFromJsonAsync<TelegramUpdatesResponse>(path, cancellationToken: ct);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Falha ao sincronizar updates do Telegram.");
            return;
        }

        if (response?.Ok != true || response.Result is null || response.Result.Count == 0)
            return;

        long lastUpdateId = settings.LastUpdateId ?? 0;
        foreach (var update in response.Result)
        {
            lastUpdateId = Math.Max(lastUpdateId, update.UpdateId);
            await ProcessUpdateAsync(settings, update, ct);
        }

        await SaveLastUpdateIdAsync(lastUpdateId, ct);
    }

    private async Task ProcessUpdateAsync(TelegramSettings settings, TelegramUpdate update, CancellationToken ct)
    {
        var message = update.Message;
        if (message == null || message.Chat?.Id == null)
            return;

        if (!string.IsNullOrWhiteSpace(message.Text) && message.Text.StartsWith("/start", StringComparison.OrdinalIgnoreCase))
        {
            var payload = message.Text.Length > 6
                ? message.Text[6..].Trim()
                : string.Empty;
            await ProcessStartCommandAsync(settings, payload, message, ct);
            return;
        }

        if (message.Contact?.PhoneNumber is not null)
            await ProcessContactShareAsync(settings, message, ct);
    }

    private async Task ProcessStartCommandAsync(TelegramSettings settings, string payload, TelegramMessage message, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var chatId = message.Chat?.Id;
        if (string.IsNullOrWhiteSpace(chatId))
            return;

        User? user;
        if (!string.IsNullOrWhiteSpace(payload))
        {
            user = await _identityDb.Users.SingleOrDefaultAsync(
                x => x.TelegramPendingVerificationToken == payload
                     && x.TelegramPendingVerificationExpiresAt.HasValue
                     && x.TelegramPendingVerificationExpiresAt > now,
                ct);
        }
        else
        {
            user = await _identityDb.Users.SingleOrDefaultAsync(
                x => x.TelegramChatId == chatId
                     && x.TelegramPendingVerificationExpiresAt.HasValue
                     && x.TelegramPendingVerificationExpiresAt > now,
                ct);
        }

        if (user == null)
        {
            if (string.IsNullOrWhiteSpace(payload))
            {
                await SendContactRequestAsync(settings.BotToken, chatId, null, ct);
                await SendTextMessageAsync(
                    settings.BotToken,
                    chatId,
                    "Se tens um pedido de validacao ativo na Wekaza, partilha o teu contacto aqui para eu tentar associar o numero automaticamente. Se ainda nao pediste a validacao no perfil, faz isso primeiro e depois volta a esta conversa.",
                    ct);
            }

            return;
        }

        user.TelegramChatId = chatId;
        user.TelegramUsername = message.From?.Username;
        user.TelegramLinkedAt ??= now;
        user.TelegramPendingVerificationError = null;
        await _identityDb.SaveChangesAsync(ct);

        await SendContactRequestAsync(settings.BotToken, chatId, user.PendingPhoneNumber ?? user.PhoneNumber, ct);
    }

    private async Task ProcessContactShareAsync(TelegramSettings settings, TelegramMessage message, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var chatId = message.Chat?.Id;
        if (string.IsNullOrWhiteSpace(chatId))
            return;

        var sharedDigits = DigitsOnly(message.Contact!.PhoneNumber);
        if (string.IsNullOrWhiteSpace(sharedDigits))
            return;

        var user = await _identityDb.Users.SingleOrDefaultAsync(
            x => x.TelegramChatId == chatId
                 && x.TelegramPendingVerificationExpiresAt.HasValue
                 && x.TelegramPendingVerificationExpiresAt > now,
            ct);

        if (user == null)
        {
            var matchingUsers = await _identityDb.Users
                .Where(x => x.TelegramPendingVerificationExpiresAt.HasValue
                            && x.TelegramPendingVerificationExpiresAt > now
                            && x.TelegramPendingExpectedPhoneNumber != null)
                .ToListAsync(ct);

            var matchedUsers = matchingUsers
                .Where(x => DigitsOnly(x.TelegramPendingExpectedPhoneNumber) == sharedDigits)
                .Take(2)
                .ToList();

            if (matchedUsers.Count != 1)
            {
                await SendTextMessageAsync(
                    settings.BotToken,
                    chatId,
                    "Nao consegui associar este contacto a um pedido ativo de validacao. Volta ao perfil, pede uma nova validacao e abre o bot pelo link ou QR code antes de partilhar o contacto.",
                    ct);
                return;
            }

            user = matchedUsers[0];
            user.TelegramChatId = chatId;
            user.TelegramUsername = message.From?.Username;
            user.TelegramLinkedAt ??= now;
            user.TelegramPendingVerificationError = null;
        }

        if (HasLockedVerifiedPhone(user))
        {
            user.PendingPhoneCountryCode = null;
            user.PendingPhoneNumber = null;
            user.PendingPhoneContactPlatform = null;
            ClearTelegramPendingVerification(user);
            await _identityDb.SaveChangesAsync(ct);
            await SendTextMessageAsync(
                settings.BotToken,
                chatId,
                "Este contacto ja foi validado e ficou bloqueado na tua conta Wekaza. Se precisares de alterar o numero, contacta o suporte.",
                ct);
            return;
        }

        var expectedDigits = DigitsOnly(user.TelegramPendingExpectedPhoneNumber);

        if (string.IsNullOrWhiteSpace(expectedDigits) || sharedDigits != expectedDigits)
        {
            user.TelegramPendingVerificationError = "O contacto partilhado no Telegram nao coincide com o numero configurado no perfil. Atualiza o numero ou partilha o contacto correto para continuar.";
            await _identityDb.SaveChangesAsync(ct);
            await SendTextMessageAsync(
                settings.BotToken,
                chatId,
                "O contacto partilhado nao coincide com o numero guardado no teu perfil Wekaza. Atualiza o numero no perfil e tenta novamente.",
                ct);
            return;
        }

        PromotePendingPhone(user);
        user.IsPhoneNumberVerified = true;
        user.PhoneNumberVerifiedAt = now;
        ClearTelegramPendingVerification(user);
        user.TelegramLinkedAt ??= now;
        user.TelegramUsername = message.From?.Username ?? user.TelegramUsername;
        await _identityDb.SaveChangesAsync(ct);

        await SendTextMessageAsync(
            settings.BotToken,
            chatId,
            "Numero validado com sucesso. Ja podes usar este contacto para login e notificacoes na Wekaza.",
            ct);
    }

    private async Task SendContactRequestAsync(string botToken, string chatId, string? phoneNumber, CancellationToken ct)
    {
        var text = string.IsNullOrWhiteSpace(phoneNumber)
            ? "Partilha o teu contacto no Telegram para ligar este bot a tua conta Wekaza."
            : $"Partilha o teu contacto no Telegram para validar o numero {phoneNumber} na tua conta Wekaza.";

        var payload = new
        {
            chat_id = chatId,
            text,
            reply_markup = new
            {
                keyboard = new[]
                {
                    new[]
                    {
                        new { text = "Partilhar contacto", request_contact = true }
                    }
                },
                resize_keyboard = true,
                one_time_keyboard = true
            }
        };

        await PostTelegramAsync(botToken, "sendMessage", payload, ct);
    }

    private async Task SendTextMessageAsync(string botToken, string chatId, string text, CancellationToken ct)
        => await PostTelegramAsync(botToken, "sendMessage", new { chat_id = chatId, text }, ct);

    private async Task PostTelegramAsync(string botToken, string method, object payload, CancellationToken ct)
    {
        var response = await _httpClient.PostAsJsonAsync($"/bot{botToken}/{method}", payload, ct);
        if (response.IsSuccessStatusCode)
            return;

        var body = await response.Content.ReadAsStringAsync(ct);
        throw new InvalidOperationException($"Telegram Bot API devolveu {(int)response.StatusCode}: {body}");
    }

    private async Task<TelegramSettings> GetConfiguredSettingsAsync(CancellationToken ct)
        => await TryGetSettingsAsync(ct)
           ?? throw new InvalidOperationException("O Telegram da plataforma ainda não está configurado no backoffice.");

    private async Task<TelegramSettings?> TryGetSettingsAsync(CancellationToken ct)
    {
        var settings = await _adminDb.PlatformSettings
            .Where(x => x.Key == BotTokenSettingKey || x.Key == BotUsernameSettingKey || x.Key == LastUpdateIdSettingKey)
            .ToListAsync(ct);

        var botToken = settings.FirstOrDefault(x => x.Key == BotTokenSettingKey)?.Value?.Trim();
        var botUsername = settings.FirstOrDefault(x => x.Key == BotUsernameSettingKey)?.Value?.Trim().TrimStart('@');
        var lastUpdateRaw = settings.FirstOrDefault(x => x.Key == LastUpdateIdSettingKey)?.Value;
        var lastUpdateId = long.TryParse(lastUpdateRaw, out var parsedLastUpdateId)
            ? parsedLastUpdateId
            : (long?)null;

        if (string.IsNullOrWhiteSpace(botToken) || string.IsNullOrWhiteSpace(botUsername))
            return null;

        return new TelegramSettings(botToken, botUsername, lastUpdateId);
    }

    private async Task SaveLastUpdateIdAsync(long lastUpdateId, CancellationToken ct)
    {
        var setting = await _adminDb.PlatformSettings.FirstOrDefaultAsync(x => x.Key == LastUpdateIdSettingKey, ct);
        if (setting == null)
        {
            setting = new PlatformSetting
            {
                Key = LastUpdateIdSettingKey,
                Category = "communications",
                Description = "Último update processado do Telegram Bot API.",
                ValueType = "number"
            };
            _adminDb.PlatformSettings.Add(setting);
        }

        setting.Value = lastUpdateId.ToString();
        setting.UpdatedAt = DateTime.UtcNow;
        await _adminDb.SaveChangesAsync(ct);
    }

    private static string BuildDeepLink(string botUsername, string payload)
        => $"https://t.me/{botUsername}?start={payload}";

    private static bool HasPendingPhone(User user)
        => !string.IsNullOrWhiteSpace(user.PendingPhoneNumber);

    private static bool HasLockedVerifiedPhone(User user)
        => user.IsPhoneNumberVerified && !string.IsNullOrWhiteSpace(user.PhoneNumber);

    private static void PromotePendingPhone(User user)
    {
        if (!HasPendingPhone(user))
            return;

        user.PhoneCountryCode = user.PendingPhoneCountryCode;
        user.PhoneNumber = user.PendingPhoneNumber;
        user.PhoneContactPlatform = PhoneContactPlatforms.Normalize(user.PendingPhoneContactPlatform, PhoneContactPlatforms.Telegram);
        user.PendingPhoneCountryCode = null;
        user.PendingPhoneNumber = null;
        user.PendingPhoneContactPlatform = null;
    }

    private static void ClearTelegramPendingVerification(User user)
    {
        user.TelegramPendingVerificationToken = null;
        user.TelegramPendingExpectedPhoneNumber = null;
        user.TelegramPendingVerificationExpiresAt = null;
        user.TelegramPendingVerificationError = null;
    }

    private static string NormalizePhoneNumber(string phoneNumber)
    {
        var digits = DigitsOnly(phoneNumber);
        if (string.IsNullOrWhiteSpace(digits))
            throw new InvalidOperationException("Número de telemóvel inválido.");

        return $"+{digits}";
    }

    private static string DigitsOnly(string? value)
        => new string((value ?? string.Empty).Where(char.IsDigit).ToArray());

    private sealed record TelegramSettings(string BotToken, string BotUsername, long? LastUpdateId);

    private sealed class TelegramUpdatesResponse
    {
        [JsonPropertyName("ok")]
        public bool Ok { get; set; }

        [JsonPropertyName("result")]
        public List<TelegramUpdate> Result { get; set; } = [];
    }

    private sealed class TelegramUpdate
    {
        [JsonPropertyName("update_id")]
        public long UpdateId { get; set; }

        [JsonPropertyName("message")]
        public TelegramMessage? Message { get; set; }
    }

    private sealed class TelegramMessage
    {
        [JsonPropertyName("text")]
        public string? Text { get; set; }

        [JsonPropertyName("chat")]
        public TelegramChat? Chat { get; set; }

        [JsonPropertyName("from")]
        public TelegramUser? From { get; set; }

        [JsonPropertyName("contact")]
        public TelegramContact? Contact { get; set; }
    }

    private sealed class TelegramChat
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = string.Empty;
    }

    private sealed class TelegramUser
    {
        [JsonPropertyName("username")]
        public string? Username { get; set; }
    }

    private sealed class TelegramContact
    {
        [JsonPropertyName("phone_number")]
        public string PhoneNumber { get; set; } = string.Empty;
    }
}