using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

internal static class ChoreMappings
{
    public static ChoreListItemDto ToListItem(ChoreDefinition chore) => new(
        chore.Id,
        chore.Title,
        chore.RoomId,
        chore.IsActive,
        chore.EstimatedMinutes,
        chore.Frequency,
        chore.Interval,
        chore.Mode,
        chore.AssignmentPolicy,
        chore.FixedAssigneeMemberId,
        chore.LastCompletedDate,
        chore.NextOccurrenceDate,
        chore.NextDueDate,
        chore.PlannedMemberId,
        chore.IsLocked,
        chore.ScheduleState,
        chore.UnscheduledReason);

    public static ChoreDetailDto ToDetail(ChoreDefinition chore, IReadOnlyList<ChoreCompletionDto> recentCompletions) => new(
        chore.Id,
        chore.HouseholdId,
        chore.Title,
        chore.Description,
        chore.RoomId,
        chore.IsActive,
        chore.EstimatedMinutes,
        chore.EffortWeight,
        chore.PreferSameRoomGrouping,
        chore.Frequency,
        chore.Interval,
        chore.Mode,
        chore.AnchorDate,
        chore.AssignmentPolicy,
        chore.FixedAssigneeMemberId,
        chore.LastCompletedDate,
        chore.LastCompletedByMemberId,
        chore.NextOccurrenceDate,
        chore.NextDueDate,
        chore.PlannedMemberId,
        chore.IsLocked,
        chore.ScheduleState,
        chore.UnscheduledReason,
        chore.CreatedAtUtc,
        chore.UpdatedAtUtc,
        recentCompletions);

    public static ChoreCompletionDto ToDto(CompletionHistory history) => new(
        history.Id,
        history.CompletedDate,
        history.RecordedAtUtc,
        history.Source,
        history.CompletedByMemberId);
}
