namespace HomeDaemon.Application.Budget;

public sealed record CreateBudgetSubcategoryCommand(Guid HouseholdId, Guid CategoryId, string Name, int SortOrder);
