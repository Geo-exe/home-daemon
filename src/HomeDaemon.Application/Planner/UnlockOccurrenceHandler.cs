using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Chores;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Sync;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Planner;

public sealed class UnlockOccurrenceHandler(
    IApplicationDbContext db,
    ChoreSyncService sync,
    TimeProvider timeProvider)
{
    public async Task<ChoreDetailDto> HandleAsync(UnlockOccurrenceCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chore = await db.Chores
            .FirstOrDefaultAsync(item => item.Id == command.ChoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Chore '{command.ChoreId}' was not found.");

        chore.IsLocked = false;
        chore.NextDueDate = null;
        chore.PlannedMemberId = null;
        chore.ScheduleState = ChoreScheduleState.Unplanned;
        chore.UnscheduledReason = null;
        chore.UpdatedAtUtc = timeProvider.GetUtcNow();

        await sync.EnqueueAsync(chore.HouseholdId, [chore], cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var completions = await ChoreHistoryReader.RecentAsync(db, chore.Id, cancellationToken).ConfigureAwait(false);
        return ChoreMappings.ToDetail(chore, completions);
    }
}
