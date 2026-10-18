namespace HomeDaemon.Domain.Budget;

/// <summary>
/// The net monthly income one person contributes to the settlement year. A missing row means
/// "not entered"; a row with a zero amount means the income was genuinely nothing.
/// </summary>
public sealed class BudgetIncome
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid MemberId { get; set; }

    /// <summary>Calendar year the income belongs to, for example 2026.</summary>
    public int Year { get; set; }

    /// <summary>Month of the year, 1 through 12.</summary>
    public int Month { get; set; }

    /// <summary>Net amount in minor currency units (cents). Never negative.</summary>
    public long AmountMinor { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
