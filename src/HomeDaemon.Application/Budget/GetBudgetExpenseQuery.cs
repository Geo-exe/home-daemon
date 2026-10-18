namespace HomeDaemon.Application.Budget;

public sealed record GetBudgetExpenseQuery(Guid HouseholdId, Guid ExpenseId);
