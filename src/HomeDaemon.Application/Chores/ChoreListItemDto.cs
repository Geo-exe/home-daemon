using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

public sealed record ChoreListItemDto(
    Guid Id,
    string Title,
    Guid? RoomId,
    bool IsActive,
    int EstimatedMinutes,
    RecurrenceFrequency Frequency,
    int Interval,
    RecurrenceMode Mode,
    AssignmentPolicy AssignmentPolicy,
    Guid? FixedAssigneeMemberId,
    DateOnly? LastCompletedDate,
    DateOnly? NextOccurrenceDate,
    DateOnly? NextDueDate,
    Guid? PlannedMemberId,
    bool IsLocked,
    ChoreScheduleState ScheduleState,
    string? UnscheduledReason);
