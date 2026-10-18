namespace HomeDaemon.Application.Budget;

public sealed record CreateBudgetTransferCommand(
    Guid HouseholdId,
    DateOnly Date,
    int SettlementYear,
    long AmountMinor,
    Guid FromMemberId,
    Guid ToMemberId,
    string? Description);
