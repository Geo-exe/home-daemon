using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Sync;

namespace HomeDaemon.Application.Sync;

/// <summary>
/// Writes sync operations into the outbox. It must be called inside the same unit of work
/// that persists the plan so that commit and dispatch stay consistent.
/// </summary>
public static class TodoSyncOutboxWriter
{
    public static void Enqueue(
        IApplicationDbContext db,
        Guid householdId,
        DateTimeOffset createdAtUtc,
        IReadOnlyList<TodoSyncOperation> operations)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(operations);

        foreach (var operation in operations)
        {
            db.TodoSyncOutbox.Add(new TodoSyncOutbox
            {
                HouseholdId = householdId,
                ChoreDefinitionId = operation.ChoreId == Guid.Empty ? null : operation.ChoreId,
                OperationType = operation.OperationType,
                TodoListEntityId = operation.TodoListEntityId,
                ItemUid = operation.ItemUid,
                Payload = operation.Payload,
                Status = TodoSyncStatus.Pending,
                CreatedAtUtc = createdAtUtc,
            });
        }
    }
}
