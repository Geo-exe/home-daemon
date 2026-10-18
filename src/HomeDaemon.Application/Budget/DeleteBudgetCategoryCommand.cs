namespace HomeDaemon.Application.Budget;

public sealed record DeleteBudgetCategoryCommand(Guid HouseholdId, Guid CategoryId);
