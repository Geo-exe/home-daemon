namespace HomeDaemon.Domain.Budget;

/// <summary>
/// Second level of the budget category tree. A subcategory always belongs to exactly one
/// category; an expense may reference one or none.
/// </summary>
public sealed class BudgetSubcategory
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid CategoryId { get; set; }

    public string Name { get; set; } = string.Empty;

    public int SortOrder { get; set; }

    public bool IsArchived { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
