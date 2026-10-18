namespace HomeDaemon.Application.Budget;

public sealed record UpdateBudgetTransferCommand(
    Guid HouseholdId,
    Guid TransferId,
    DateOnly Date,
    int SettlementYear,
    long AmountMinor,
    Guid FromMemberId,
    Guid ToMemberId,
    string? Description);
