namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Result of the availability calculation for one member and one civil day.
/// </summary>
public sealed record DailyAvailability(
    DateOnly Date,
    Guid MemberId,
    int WindowMinutes,
    int OccupiedMinutes,
    int FreeMinutes,
    bool IsAllDayBlocked);
