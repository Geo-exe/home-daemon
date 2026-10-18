namespace HomeDaemon.Application.Budget;

public sealed record BudgetMemberSummaryDto(
    Guid MemberId,
    string Name,
    long AnnualIncomeMinor,
    decimal? Share,
    long PaidMinor,
    long? DueMinor,
    long TransferredOutMinor,
    long TransferredInMinor,
    long? BalanceMinor,
    IReadOnlyList<int> MissingMonths);
