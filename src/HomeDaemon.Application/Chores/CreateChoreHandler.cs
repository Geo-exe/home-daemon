using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

public sealed class CreateChoreHandler(
    IApplicationDbContext db,
    IChoreRecurrenceService recurrence,
    TimeProvider timeProvider)
{
    public async Task<ChoreDetailDto> HandleAsync(CreateChoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ChoreValidation.Validate(command.Title, command.EstimatedMinutes, command.Interval, command.EffortWeight);

        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        await ChoreValidation.EnsureRoomBelongsToHouseholdAsync(db, household.Id, command.RoomId, cancellationToken).ConfigureAwait(false);
        EnsureAssignee(command.AssignmentPolicy, command.FixedAssigneeMemberId);

        var now = timeProvider.GetUtcNow();
        var chore = new ChoreDefinition
        {
            HouseholdId = household.Id,
            Title = command.Title.Trim(),
            Description = string.IsNullOrWhiteSpace(command.Description) ? null : command.Description.Trim(),
            RoomId = command.RoomId,
            IsActive = command.IsActive,
            EstimatedMinutes = command.EstimatedMinutes,
            EffortWeight = command.EffortWeight,
            PreferSameRoomGrouping = command.PreferSameRoomGrouping,
            Frequency = command.Frequency,
            Interval = command.Interval,
            Mode = command.Mode,
            AnchorDate = command.AnchorDate,
            AssignmentPolicy = command.AssignmentPolicy,
            FixedAssigneeMemberId = command.FixedAssigneeMemberId,
            ScheduleState = ChoreScheduleState.Unplanned,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        chore.NextOccurrenceDate = recurrence.GetInitialOccurrence(chore, HouseholdClock.Today(household, timeProvider));
        if (!chore.IsActive)
        {
            chore.ScheduleState = ChoreScheduleState.Paused;
        }

        db.Chores.Add(chore);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return ChoreMappings.ToDetail(chore, []);
    }

    private static void EnsureAssignee(AssignmentPolicy policy, Guid? fixedAssigneeMemberId)
    {
        if (policy == AssignmentPolicy.SpecificMember && fixedAssigneeMemberId is null)
        {
            throw new ApplicationValidationException("A fixed assignee is required for the specific-member policy.");
        }
    }
}
