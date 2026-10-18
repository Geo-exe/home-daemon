namespace HomeDaemon.Application.Budget;

public sealed record DeleteBudgetSubcategoryCommand(Guid HouseholdId, Guid SubcategoryId);
