namespace HomeDaemon.Application.Budget;

public sealed record GetBudgetTransfersQuery(Guid HouseholdId, int? SettlementYear, string? Cursor, int? PageSize);
