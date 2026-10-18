using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Sync;

/// <summary>
/// Reconciles the desired plan with Home Assistant by writing the diff into the outbox.
/// It loads the household's members, rooms and existing links so callers only pass the
/// chores they changed.
/// </summary>
public sealed class ChoreSyncService(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task EnqueueAsync(Guid householdId, IReadOnlyList<ChoreDefinition> chores, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(chores);

        if (chores.Count == 0)
        {
            return;
        }

        var members = await db.HouseholdMembers
            .Where(member => member.HouseholdId == householdId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var rooms = await db.Rooms
            .Where(room => room.HouseholdId == householdId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var choreIds = chores.Select(chore => chore.Id).ToList();
        var links = await db.TodoItemLinks
            .Where(link => choreIds.Contains(link.ChoreDefinitionId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var roomNames = rooms.ToDictionary(room => room.Id, room => room.Name);
        var operations = TodoSyncPlanner.BuildPlan(chores, members, roomNames, links);
        TodoSyncOutboxWriter.Enqueue(db, householdId, timeProvider.GetUtcNow(), operations);
    }
}
