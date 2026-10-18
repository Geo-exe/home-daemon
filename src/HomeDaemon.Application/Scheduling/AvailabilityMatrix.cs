namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Availability resolved for every member and civil day of a range, plus diagnostics that
/// explain any calendar source that could not be read.
/// </summary>
public sealed record AvailabilityMatrix(
    IReadOnlyDictionary<(DateOnly Date, Guid MemberId), DailyAvailability> Availability,
    IReadOnlyList<string> Diagnostics,
    bool CalendarDataAvailable);
