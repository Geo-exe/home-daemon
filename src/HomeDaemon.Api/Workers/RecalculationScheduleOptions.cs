namespace HomeDaemon.Api.Workers;

public sealed class RecalculationScheduleOptions
{
    /// <summary>Local time of day (household timezone) at which the nightly recalculation runs, for example <c>02:00</c>.</summary>
    public TimeOnly NightlyRunTime { get; set; } = new(2, 0);
}
