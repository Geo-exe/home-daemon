namespace HomeDaemon.Domain.Budget;

/// <summary>
/// A shared expense. Every expense is split across the budget participants in proportion to
/// their annual income, so it always records who actually paid it.
/// </summary>
public sealed class BudgetExpense
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    /// <summary>Local calendar date of the expense; its year is the settlement year.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Amount in minor currency units (cents). Always positive.</summary>
    public long AmountMinor { get; set; }

    public Guid CategoryId { get; set; }

    /// <summary>Optional; when set it must belong to <see cref="CategoryId"/>.</summary>
    public Guid? SubcategoryId { get; set; }

    public Guid PaidByMemberId { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
