namespace HomeDaemon.Application.Budget;

public sealed record UpdateBudgetCategoryCommand(Guid HouseholdId, Guid CategoryId, string Name, int SortOrder, bool IsArchived);
