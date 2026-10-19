using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// One already-normalized chore row from an external export. Recurrence values and member ids
/// are resolved before reaching the application layer; dates are calendar dates.
/// </summary>
public sealed record ImportChoreRow(
    int Line,
    string SourceId,
    string Title,
    string? Description,
    string? Room,
    int EstimatedMinutes,
    bool IsActive,
    double? EffortWeight,
    bool PreferSameRoomGrouping,
    RecurrenceFrequency Frequency,
    int Interval,
    RecurrenceMode Mode,
    DateOnly? AnchorDate,
    AssignmentPolicy AssignmentPolicy,
    Guid? FixedAssigneeMemberId,
    DateOnly? SyntheticCompletionDate,
    IReadOnlyList<DateOnly> Completions);
