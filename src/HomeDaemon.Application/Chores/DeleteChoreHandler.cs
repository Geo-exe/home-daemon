using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Sync;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

public sealed class DeleteChoreHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task HandleAsync(DeleteChoreCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var chore = await db.Chores
            .FirstOrDefaultAsync(item => item.Id == command.ChoreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Chore '{command.ChoreId}' was not found.");

        var links = await db.TodoItemLinks
            .Where(link => link.ChoreDefinitionId == chore.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Stale operations for this chore would fail once it is gone; drop them and queue
        // explicit deletions so the Home Assistant items are not orphaned.
        var staleOperations = await db.TodoSyncOutbox
            .Where(item => item.ChoreDefinitionId == chore.Id && item.Status == TodoSyncStatus.Pending)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var operation in staleOperations)
        {
            db.TodoSyncOutbox.Remove(operation);
        }

        var now = timeProvider.GetUtcNow();
        foreach (var link in links)
        {
            db.TodoSyncOutbox.Add(new TodoSyncOutbox
            {
                HouseholdId = chore.HouseholdId,
                ChoreDefinitionId = null,
                OperationType = TodoSyncOperationType.Delete,
                TodoListEntityId = link.TodoListEntityId,
                ItemUid = link.ItemUid,
                Payload = "{}",
                Status = TodoSyncStatus.Pending,
                CreatedAtUtc = now,
            });
        }

        // Soft delete: the chore row is kept so the completion history and its foreign keys
        // stay valid. The global query filter hides it from catalog, planner and scheduling.
        // The Home Assistant mapping and the queued deletions are committed in the same
        // transaction as the flag, so a retry can never lose the link between the two.
        db.TodoItemLinks.RemoveRange(links);
        chore.DeletedAtUtc = now;
        chore.UpdatedAtUtc = now;

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
