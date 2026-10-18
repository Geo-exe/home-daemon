using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

public sealed record UpdateChoreCommand(
    Guid ChoreId,
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
    Guid? FixedAssigneeMemberId);
