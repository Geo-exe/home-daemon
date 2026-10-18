using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Scheduling;
using HomeDaemon.Domain.Scheduling;
using Microsoft.Extensions.Options;

namespace HomeDaemon.Api.Workers;

/// <summary>Triggers the shared recalculation case at the configured local time each day.</summary>
public sealed class NightlySchedulingWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<RecalculationScheduleOptions> scheduleOptions,
    TimeProvider timeProvider,
    ILogger<NightlySchedulingWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = await ComputeDelaySafeAsync(stoppingToken).ConfigureAwait(false);
            try
            {
                await Task.Delay(delay, timeProvider, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            try
            {
                using var scope = scopeFactory.CreateScope();
                var handler = scope.ServiceProvider.GetRequiredService<RecalculateScheduleCommandHandler>();
                await handler.HandleAsync(new RecalculateScheduleCommand(ScheduleRunTrigger.Nightly), stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ApiLog.NightlyRecalculationFailed(logger, ex);
            }
        }
    }

    private async Task<TimeSpan> ComputeDelaySafeAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await ComputeDelayAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            ApiLog.NightlyDelayFailed(logger, ex);
            return TimeSpan.FromMinutes(1);
        }
    }

    private async Task<TimeSpan> ComputeDelayAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var household = await HouseholdLookup.FindAsync(db, cancellationToken).ConfigureAwait(false);
        var timeZone = household is null ? TimeZoneInfo.Utc : HouseholdClock.TimeZone(household);
        return WorkerSchedule.ComputeDelay(timeProvider.GetUtcNow(), timeZone, scheduleOptions.Value.NightlyRunTime);
    }
}
