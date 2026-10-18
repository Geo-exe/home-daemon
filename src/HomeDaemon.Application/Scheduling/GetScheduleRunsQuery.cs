namespace HomeDaemon.Application.Scheduling;

/// <summary>Query for the paginated list of recalculation runs, newest first.</summary>
public sealed record GetScheduleRunsQuery(Guid HouseholdId, string? Cursor, int? PageSize);
