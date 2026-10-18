namespace HomeDaemon.Application.Budget;

public sealed record DeleteBudgetExpenseCommand(Guid HouseholdId, Guid ExpenseId);
