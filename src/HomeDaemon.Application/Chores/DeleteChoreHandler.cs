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

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await db.TodoItemLinks.Where(link => link.ChoreDefinitionId == chore.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.CompletionHistory.Where(history => history.ChoreDefinitionId == chore.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
        await db.Chores.Where(item => item.Id == chore.Id).ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }
}
