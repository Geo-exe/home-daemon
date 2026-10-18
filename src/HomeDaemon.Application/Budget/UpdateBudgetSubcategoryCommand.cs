namespace HomeDaemon.Application.Budget;

public sealed record UpdateBudgetSubcategoryCommand(Guid HouseholdId, Guid SubcategoryId, string Name, int SortOrder, bool IsArchived);
