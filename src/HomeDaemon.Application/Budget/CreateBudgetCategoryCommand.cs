namespace HomeDaemon.Application.Budget;

public sealed record CreateBudgetCategoryCommand(Guid HouseholdId, string Name, int SortOrder);
