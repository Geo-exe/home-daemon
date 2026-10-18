namespace HomeDaemon.Api.Workers;

public sealed class BackupScheduleOptions
{
    /// <summary>Local time of day (household timezone) at which the daily backup runs, for example <c>03:00</c>.</summary>
    public TimeOnly DailyRunTime { get; set; } = new(3, 0);

    /// <summary>Set to false to disable the backup worker entirely.</summary>
    public bool Enabled { get; set; } = true;
}
