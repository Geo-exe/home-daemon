namespace HomeDaemon.Domain.Chores;

/// <summary>
/// Immutable completion event. The history is never deleted when the definition changes,
/// so statistics keep working across recipe modifications.
/// </summary>
public sealed class CompletionHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid ChoreDefinitionId { get; set; }

    /// <summary>Local household date on which the chore was actually done.</summary>
    public DateOnly CompletedDate { get; set; }

    /// <summary>Technical UTC timestamp of when the completion was recorded.</summary>
    public DateTimeOffset RecordedAtUtc { get; set; }

    public CompletionSource Source { get; set; }

    /// <summary>
    /// Caller-provided key that makes repeated notifications idempotent.
    /// Null when the caller did not supply one.
    /// </summary>
    public string? IdempotencyKey { get; set; }

    public Guid? CompletedByMemberId { get; set; }

    /// <summary>
    /// Login account that recorded a manual completion. Null for Home Assistant callbacks,
    /// which carry no user identity.
    /// </summary>
    public Guid? CompletedByUserId { get; set; }
}
