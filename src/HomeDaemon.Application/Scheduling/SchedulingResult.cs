namespace HomeDaemon.Application.Scheduling;

public sealed record SchedulingResult(
    IReadOnlyList<ChoreAssignment> Assignments,
    IReadOnlyList<UnscheduledChore> Unscheduled,
    IReadOnlyDictionary<(DateOnly Date, Guid MemberId), int> RemainingFreeMinutes);
