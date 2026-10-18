namespace HomeDaemon.Api.Endpoints;

public sealed record CreateBudgetTransferRequest(
    DateOnly Date,
    int SettlementYear,
    long AmountMinor,
    Guid FromMemberId,
    Guid ToMemberId,
    string? Description);
