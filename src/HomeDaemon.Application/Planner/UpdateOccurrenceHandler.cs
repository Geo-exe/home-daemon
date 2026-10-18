using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Sync;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Planner;

public sealed class UpdateOccurrenceHandler(
    IApplicationDbContext db,
    ChoreSyncService sync,
    TimeProvider timeProvider)
{
    public async Task<ChoreDetailDto> HandleAsync(UpdateOccurrenceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chore = await db.Chores
            .FirstOrDefaultAsync(item => item.Id == command.ChoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Chore '{command.ChoreId}' was not found.");

        var memberExists = await db.HouseholdMembers
            .AsNoTracking()
            .AnyAsync(member => member.Id == command.MemberId && member.HouseholdId == chore.HouseholdId, cancellationToken)
            .ConfigureAwait(false);

        if (!memberExists)
        {
            throw new ApplicationValidationException($"Member '{command.MemberId}' does not belong to the chore household.");
        }

        chore.NextDueDate = command.Date;
        chore.PlannedMemberId = command.MemberId;
        chore.IsLocked = true;
        chore.ScheduleState = ChoreScheduleState.Scheduled;
        chore.UnscheduledReason = null;
        chore.UpdatedAtUtc = timeProvider.GetUtcNow();

        await sync.EnqueueAsync(chore.HouseholdId, [chore], cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var completions = await ChoreHistoryReader.RecentAsync(db, chore.Id, cancellationToken).ConfigureAwait(false);
        return ChoreMappings.ToDetail(chore, completions);
    }
}
