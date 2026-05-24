using TrustRent.Modules.Identity.Contracts.Interfaces;

namespace TrustRent.Api.Services;

public sealed class TelegramUpdatesPollingService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(3);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TelegramUpdatesPollingService> _logger;

    public TelegramUpdatesPollingService(IServiceScopeFactory scopeFactory, ILogger<TelegramUpdatesPollingService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var telegramService = scope.ServiceProvider.GetRequiredService<ITelegramMessagingPlatformService>();
                await telegramService.SyncPendingUpdatesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Falha ao sincronizar updates do Telegram em background.");
            }

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                    break;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}