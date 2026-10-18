using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

public sealed record ChoreDetailDto(
    Guid Id,
    Guid HouseholdId,
    string Title,
    string? Description,
    Guid? RoomId,
    bool IsActive,
    int EstimatedMinutes,
    double? EffortWeight,
    bool PreferSameRoomGrouping,
    RecurrenceFrequency Frequency,
    int Interval,
    RecurrenceMode Mode,
    DateOnly? AnchorDate,
    AssignmentPolicy AssignmentPolicy,
    Guid? FixedAssigneeMemberId,
    DateOnly? LastCompletedDate,
    Guid? LastCompletedByMemberId,
    DateOnly? NextOccurrenceDate,
    DateOnly? NextDueDate,
    Guid? PlannedMemberId,
    bool IsLocked,
    ChoreScheduleState ScheduleState,
    string? UnscheduledReason,
    DateTimeOffset CreatedAtUtc,
    DateTimeOffset UpdatedAtUtc,
    IReadOnlyList<ChoreCompletionDto> RecentCompletions);
