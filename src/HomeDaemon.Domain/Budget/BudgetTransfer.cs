namespace HomeDaemon.Domain.Budget;

/// <summary>
/// A real money movement between two participants that settles part of a year's balance.
/// The movement date and the settlement year are distinct: a transfer made in January can
/// settle the previous year without affecting the new one.
/// </summary>
public sealed class BudgetTransfer
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    /// <summary>Calendar date on which the money actually moved.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Year of the balance this transfer settles.</summary>
    public int SettlementYear { get; set; }

    /// <summary>Amount in minor currency units (cents). Always positive.</summary>
    public long AmountMinor { get; set; }

    public Guid FromMemberId { get; set; }

    public Guid ToMemberId { get; set; }

    public string? Description { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
