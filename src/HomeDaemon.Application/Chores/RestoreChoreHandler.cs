using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// Undoes a soft delete. Restoring is always explicit; a deleted chore is never brought back
/// as a side effect of another operation. The plan is reset so the next recalculation places
/// the chore again from scratch.
/// </summary>
public sealed class RestoreChoreHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<ChoreDetailDto> HandleAsync(RestoreChoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chore = await db.Chores
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(item => item.Id == command.ChoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Chore '{command.ChoreId}' was not found.");

        if (chore.DeletedAtUtc is null)
        {
            throw new EntityConflictException($"Chore '{command.ChoreId}' is not deleted.");
        }

        chore.DeletedAtUtc = null;
        chore.IsLocked = false;
        chore.NextDueDate = null;
        chore.PlannedMemberId = null;
        chore.UnscheduledReason = null;
        chore.ScheduleState = chore.IsActive ? ChoreScheduleState.Unplanned : ChoreScheduleState.Paused;
        chore.UpdatedAtUtc = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var completions = await ChoreHistoryReader.RecentAsync(db, chore.Id, cancellationToken).ConfigureAwait(false);
        return ChoreMappings.ToDetail(chore, completions);
    }
}
