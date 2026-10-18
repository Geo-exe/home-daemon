namespace HomeDaemon.Application.Budget;

public sealed record DeleteBudgetIncomeCommand(Guid HouseholdId, Guid IncomeId);
