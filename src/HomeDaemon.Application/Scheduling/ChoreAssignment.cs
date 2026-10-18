namespace HomeDaemon.Application.Scheduling;

public sealed record ChoreAssignment(Guid ChoreId, DateOnly Date, Guid MemberId);
