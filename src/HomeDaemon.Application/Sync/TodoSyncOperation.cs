using HomeDaemon.Domain.Sync;

namespace HomeDaemon.Application.Sync;

/// <summary>A single desired mutation of the Home Assistant to-do state.</summary>
public sealed record TodoSyncOperation(
    Guid ChoreId,
    TodoSyncOperationType OperationType,
    string TodoListEntityId,
    string? ItemUid,
    string Payload);
