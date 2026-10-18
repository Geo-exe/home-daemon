namespace HomeDaemon.Domain.Budget;

/// <summary>
/// The balance of one participant for a settlement year. <see cref="Share"/>, <see cref="DueMinor"/>
/// and <see cref="BalanceMinor"/> are null when no income was recorded, because there is no
/// honest proportion to apply.
/// </summary>
public sealed record BudgetMemberBalance(
    Guid MemberId,
    long AnnualIncomeMinor,
    decimal? Share,
    long PaidMinor,
    long? DueMinor,
    long TransferredOutMinor,
    long TransferredInMinor,
    long? BalanceMinor);
