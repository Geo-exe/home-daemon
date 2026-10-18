namespace HomeDaemon.Domain.Budget;

/// <summary>
/// First level of the budget category tree. Categories are shared across years and are the
/// dimension every expense is reported by.
/// </summary>
public sealed class BudgetCategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    /// <summary>Archived categories stay for history but are hidden from the pickers.</summary>
    public bool IsArchived { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
