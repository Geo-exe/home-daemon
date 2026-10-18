using HomeDaemon.Application.Sync;
using Microsoft.Extensions.Options;

namespace HomeDaemon.Api.Workers;

/// <summary>Periodically dispatches pending outbox entries to Home Assistant.</summary>
public sealed class TodoSyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<TodoSyncOptions> options,
    ILogger<TodoSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = TimeSpan.FromSeconds(Math.Max(5, options.Value.SyncIntervalSeconds));
        using var timer = new PeriodicTimer(interval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunOnceAsync(stoppingToken).ConfigureAwait(false);

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
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var processor = scope.ServiceProvider.GetRequiredService<TodoSyncProcessor>();
            await processor.ProcessPendingAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ApiLog.TodoSyncPassFailed(logger, ex);
        }
    }
}
