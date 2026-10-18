namespace HomeDaemon.Application.Planner;

public sealed record PlannerDto(
    DateOnly From,
    DateOnly To,
    bool CalendarDataAvailable,
    IReadOnlyList<PlannerDayDto> Days,
    IReadOnlyList<PlannerUnscheduledDto> Unscheduled);
