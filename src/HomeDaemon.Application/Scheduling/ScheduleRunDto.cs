using HomeDaemon.Domain.Scheduling;

namespace HomeDaemon.Application.Scheduling;

public sealed record ScheduleRunDto(
    Guid Id,
    ScheduleRunTrigger Trigger,
    ScheduleRunStatus Status,
    DateTimeOffset StartedAtUtc,
    DateTimeOffset? CompletedAtUtc,
    int ScheduledCount,
    int UnscheduledCount,
    string? Summary,
    string? Diagnostics,
    string? ErrorMessage);
