using HomeDaemon.Domain.Scheduling;

namespace HomeDaemon.Application.Scheduling;

internal static class ScheduleRunMappings
{
    public static ScheduleRunDto ToDto(ScheduleRun run) => new(
        run.Id,
        run.Trigger,
        run.Status,
        run.StartedAtUtc,
        run.CompletedAtUtc,
        run.ScheduledCount,
        run.UnscheduledCount,
        run.Summary,
        run.Diagnostics,
        run.ErrorMessage);
}
