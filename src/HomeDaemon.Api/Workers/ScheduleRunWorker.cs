using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Scheduling;

namespace HomeDaemon.Api.Workers;

/// <summary>
/// Drains the schedule run queue and executes recalculation. Keeping the work off the request
/// thread lets the manual endpoint answer 202 immediately.
/// </summary>
public sealed class ScheduleRunWorker(
    IServiceScopeFactory scopeFactory,
    IScheduleRunDispatcher dispatcher,
    ILogger<ScheduleRunWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Guid runId;
            try
            {
                var dequeued = await dispatcher.DequeueAsync(stoppingToken).ConfigureAwait(false);
                if (dequeued is null)
                {
                    continue;
                }

                runId = dequeued.Value;
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var executor = scope.ServiceProvider.GetRequiredService<RecalculationExecutor>();
                await executor.ExecuteAsync(runId, stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ApiLog.ScheduleRunFailed(logger, runId, ex);
            }
        }
    }
}

