namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Input of a single, deterministic scheduling pass. All external state (calendar
/// availability, locked assignments) is resolved by the caller before the engine runs.
/// </summary>
public sealed record SchedulingInput(
    DateOnly ReferenceDate,
    DateOnly HorizonEndDate,
    IReadOnlyList<ChoreCandidate> Candidates,
    IReadOnlyDictionary<(DateOnly Date, Guid MemberId), int> FreeMinutes,
    SchedulingOptions Options);
