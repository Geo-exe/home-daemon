namespace HomeDaemon.Application.Planner;

public sealed record UpdateOccurrenceCommand(Guid ChoreId, DateOnly Date, Guid MemberId);
