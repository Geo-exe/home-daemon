namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// A chore's current cycle presented to the scheduler, independent of persistence.
/// </summary>
public sealed record ChoreCandidate(
    Guid ChoreId,
    Guid? RoomId,
    int DurationMinutes,
    DateOnly DueDate,
    bool IsLocked,
    DateOnly? LockedDate,
    Guid? LockedMemberId,
    bool PreferSameRoomGrouping,
    IReadOnlyList<Guid> AllowedMemberIds,
    DateOnly? LastCompletedDate);
