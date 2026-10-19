using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// One already-normalized chore row plus its execution dates. The browser resolves rooms,
/// assignees and identities before sending the batch.
/// </summary>
public sealed record ImportChoreRowRequest(
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
    IReadOnlyList<DateOnly>? Completions);
