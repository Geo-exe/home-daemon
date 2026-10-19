using HomeDaemon.Application.Integrations.HomeAssistant;

namespace HomeDaemon.Api.Workers;

/// <summary>Drops Home Assistant call diagnostics once they pass the retention window.</summary>
public sealed class HomeAssistantLogCleanupWorker(
    IServiceScopeFactory scopeFactory,
    ILogger<HomeAssistantLogCleanupWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunOnceAsync(stoppingToken).ConfigureAwait(false);

        using var timer = new PeriodicTimer(Interval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }

            await RunOnceAsync(stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var handler = scope.ServiceProvider.GetRequiredService<CleanupHomeAssistantCallLogsHandler>();
            var removed = await handler.HandleAsync(cancellationToken).ConfigureAwait(false);

            if (removed > 0)
            {
                ApiLog.HomeAssistantLogsRemoved(logger, removed);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            ApiLog.HomeAssistantLogCleanupFailed(logger, exception);
        }
    }
}
