namespace HomeDaemon.Application.Budget;

public sealed record BudgetTransferDto(
    Guid Id,
    DateOnly Date,
    int SettlementYear,
    long AmountMinor,
    Guid FromMemberId,
    string FromMemberName,
    Guid ToMemberId,
    string ToMemberName,
    string? Description);
