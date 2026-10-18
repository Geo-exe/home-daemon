namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateBudgetTransferRequest(
    DateOnly Date,
    int SettlementYear,
    long AmountMinor,
    Guid FromMemberId,
    Guid ToMemberId,
    string? Description);
