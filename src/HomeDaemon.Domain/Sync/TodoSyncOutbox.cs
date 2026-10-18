namespace HomeDaemon.Domain.Sync;

/// <summary>
/// Persisted outbox entry. It is written inside the same transaction as the plan it
/// belongs to and dispatched after commit, so Home Assistant being offline never loses
/// or corrupts the local plan.
/// </summary>
public sealed class TodoSyncOutbox
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid? ChoreDefinitionId { get; set; }

    public TodoSyncOperationType OperationType { get; set; }

    public string TodoListEntityId { get; set; } = string.Empty;

    /// <summary>Known item UID, required for update and delete operations.</summary>
    public string? ItemUid { get; set; }

    /// <summary>Serialized operation payload, for example the item summary and due date.</summary>
    public string Payload { get; set; } = "{}";

    public TodoSyncStatus Status { get; set; } = TodoSyncStatus.Pending;

    public int Attempts { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset? ProcessedAtUtc { get; set; }

    public string? LastError { get; set; }
}
