namespace HomeDaemon.Application.Planner;

public sealed record PlannerQuery(Guid HouseholdId, DateOnly From, DateOnly To);
