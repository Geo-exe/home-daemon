namespace HomeDaemon.Application.Planner;

public sealed record PlannerUnscheduledDto(Guid ChoreId, string Title, string Reason);
