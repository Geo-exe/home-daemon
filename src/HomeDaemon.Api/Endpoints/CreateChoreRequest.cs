using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Api.Endpoints;

public sealed record CreateChoreRequest(
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
