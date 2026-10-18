namespace HomeDaemon.Domain.Households;

/// <summary>
/// A room or zone belonging to the shared core. Rooms give chores context and a soft
/// scheduling preference; they are not indissoluble groups.
/// </summary>
public sealed class Room
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
