namespace HomeDaemon.Domain.Scheduling;

/// <summary>What started a scheduling recalculation.</summary>
public enum ScheduleRunTrigger
{
    Nightly = 0,
    Manual = 1,
}
