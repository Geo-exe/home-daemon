namespace HomeDaemon.Api.Workers;

public static class WorkerSchedule
{
    /// <summary>
    /// Returns the delay until the next local <paramref name="runAt"/> in the given timezone.
    /// The computation is stateless so it stays correct across restarts and day boundaries.
    /// </summary>
    public static TimeSpan ComputeDelay(DateTimeOffset now, TimeZoneInfo timeZone, TimeOnly runAt)
    {
        ArgumentNullException.ThrowIfNull(timeZone);

        var localNow = TimeZoneInfo.ConvertTime(now, timeZone);
        var todayRun = localNow.Date + runAt.ToTimeSpan();
        var nextRun = localNow.DateTime <= todayRun ? todayRun : todayRun.AddDays(1);
        var nextRunInstant = new DateTimeOffset(nextRun, timeZone.GetUtcOffset(nextRun));
        var delay = nextRunInstant - now;

        return delay > TimeSpan.Zero ? delay : TimeSpan.FromMinutes(1);
    }
}
