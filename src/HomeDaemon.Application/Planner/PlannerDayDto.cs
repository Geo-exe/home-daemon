namespace HomeDaemon.Application.Planner;

public sealed record PlannerDayDto(
    DateOnly Date,
    IReadOnlyList<PlannerMemberDayDto> Members,
    IReadOnlyList<PlannerOccurrenceDto> Occurrences);
