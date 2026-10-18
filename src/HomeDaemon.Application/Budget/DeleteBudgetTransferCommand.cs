namespace HomeDaemon.Application.Budget;

public sealed record DeleteBudgetTransferCommand(Guid HouseholdId, Guid TransferId);
