namespace HomeDaemon.Domain.Chores;

/// <summary>
/// Maps a chore's current cycle to the Home Assistant to-do list and item UID that
/// represent it. The UID is only unique within a list/provider, so both values form
/// the logical key.
/// </summary>
public sealed class TodoItemLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid ChoreDefinitionId { get; set; }

    public string TodoListEntityId { get; set; } = string.Empty;

    public string ItemUid { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
