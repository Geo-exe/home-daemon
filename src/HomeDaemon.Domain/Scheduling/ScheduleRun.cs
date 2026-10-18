namespace HomeDaemon.Domain.Scheduling;

/// <summary>
/// Diagnostic record of a recalculation. It is retained with a bounded retention policy,
/// and it is deliberately not a general-purpose log.
/// </summary>
public sealed class ScheduleRun
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public ScheduleRunTrigger Trigger { get; set; }

    public ScheduleRunStatus Status { get; set; } = ScheduleRunStatus.Running;

    public DateTimeOffset StartedAtUtc { get; set; }

    public DateTimeOffset? CompletedAtUtc { get; set; }

    public int ScheduledCount { get; set; }

    public int UnscheduledCount { get; set; }

    /// <summary>Short human-readable summary, safe to expose through the API.</summary>
    public string? Summary { get; set; }

    /// <summary>Diagnostic collected while running, never containing secrets.</summary>
    public string? Diagnostics { get; set; }

    public string? ErrorMessage { get; set; }
}
