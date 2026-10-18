using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Application.Sync;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

public sealed class UpdateChoreHandler(
    IApplicationDbContext db,
    IChoreRecurrenceService recurrence,
    ChoreSyncService sync,
    TimeProvider timeProvider)
{
    public async Task<ChoreDetailDto> HandleAsync(UpdateChoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ChoreValidation.Validate(command.Title, command.EstimatedMinutes, command.Interval, command.EffortWeight);

        var chore = await db.Chores
            .FirstOrDefaultAsync(item => item.Id == command.ChoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Chore '{command.ChoreId}' was not found.");

        await ChoreValidation.EnsureRoomBelongsToHouseholdAsync(db, chore.HouseholdId, command.RoomId, cancellationToken).ConfigureAwait(false);

        var recurrenceChanged = HasRecurrenceChanged(chore, command);
        var scheduleAffectingChanged = recurrenceChanged || HasScheduleAffectingChanges(chore, command);

        ApplyCommandFields(chore, command);

        if (recurrenceChanged)
        {
            await RefreshOccurrenceAsync(chore, cancellationToken).ConfigureAwait(false);
        }

        InvalidateSchedule(chore, scheduleAffectingChanged);
        chore.UpdatedAtUtc = timeProvider.GetUtcNow();

        await sync.EnqueueAsync(chore.HouseholdId, [chore], cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var completions = await ChoreHistoryReader.RecentAsync(db, chore.Id, cancellationToken).ConfigureAwait(false);
        return ChoreMappings.ToDetail(chore, completions);
    }

    private async Task RefreshOccurrenceAsync(ChoreDefinition chore, CancellationToken cancellationToken)
    {
        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        var today = HouseholdClock.Today(household, timeProvider);
        chore.NextOccurrenceDate = chore.Mode == RecurrenceMode.Dynamic && chore.LastCompletedDate is DateOnly lastCompleted
            ? recurrence.GetNextAfterCompletion(chore, lastCompleted)
            : recurrence.GetInitialOccurrence(chore, today);
    }

    private static bool HasRecurrenceChanged(ChoreDefinition chore, UpdateChoreCommand command) =>
        chore.Frequency != command.Frequency
        || chore.Interval != command.Interval
        || chore.Mode != command.Mode
        || chore.AnchorDate != command.AnchorDate;

    private static bool HasScheduleAffectingChanges(ChoreDefinition chore, UpdateChoreCommand command) =>
        chore.EstimatedMinutes != command.EstimatedMinutes
        || chore.RoomId != command.RoomId
        || chore.EffortWeight != command.EffortWeight
        || chore.PreferSameRoomGrouping != command.PreferSameRoomGrouping
        || chore.AssignmentPolicy != command.AssignmentPolicy
        || chore.FixedAssigneeMemberId != command.FixedAssigneeMemberId;

    private static void ApplyCommandFields(ChoreDefinition chore, UpdateChoreCommand command)
    {
        chore.Title = command.Title.Trim();
        chore.Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim();
        chore.RoomId = command.RoomId;
        chore.IsActive = command.IsActive;
        chore.EstimatedMinutes = command.EstimatedMinutes;
        chore.EffortWeight = command.EffortWeight;
        chore.PreferSameRoomGrouping = command.PreferSameRoomGrouping;
        chore.Frequency = command.Frequency;
        chore.Interval = command.Interval;
        chore.Mode = command.Mode;
        chore.AnchorDate = command.AnchorDate;
        chore.AssignmentPolicy = command.AssignmentPolicy;
        chore.FixedAssigneeMemberId = command.FixedAssigneeMemberId;
    }

    private static void InvalidateSchedule(ChoreDefinition chore, bool scheduleAffectingChanged)
    {
        if (scheduleAffectingChanged)
        {
            chore.IsLocked = false;
            chore.NextDueDate = null;
            chore.PlannedMemberId = null;
            chore.UnscheduledReason = null;
            chore.ScheduleState = ChoreScheduleState.Unplanned;
        }

        if (!chore.IsActive)
        {
            chore.ScheduleState = ChoreScheduleState.Paused;
            chore.NextDueDate = null;
            chore.PlannedMemberId = null;
        }
    }
}
