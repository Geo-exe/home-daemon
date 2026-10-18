using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.Extensions.Options;

namespace HomeDaemon.Api.Workers;

/// <summary>
/// Runs the daily SQLite backup at the configured local time. It is independent of the
/// recalculation so a failed recalculation never prevents the backup.
/// </summary>
public sealed class DatabaseBackupWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<BackupScheduleOptions> scheduleOptions,
    TimeProvider timeProvider,
    ILogger<DatabaseBackupWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!scheduleOptions.Value.Enabled)
        {
            ApiLog.BackupDisabled(logger);
            return;
        }

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
                var backup = scope.ServiceProvider.GetRequiredService<IDatabaseBackupService>();
                await backup.CreateBackupAsync(stoppingToken).ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ApiLog.BackupFailed(logger, ex);
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
            ApiLog.BackupDelayFailed(logger, ex);
            return TimeSpan.FromMinutes(1);
        }
    }

    private async Task<TimeSpan> ComputeDelayAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IApplicationDbContext>();
        var household = await HouseholdLookup.FindAsync(db, cancellationToken).ConfigureAwait(false);
        var timeZone = household is null ? TimeZoneInfo.Utc : HouseholdClock.TimeZone(household);
        return WorkerSchedule.ComputeDelay(timeProvider.GetUtcNow(), timeZone, scheduleOptions.Value.DailyRunTime);
    }
}
