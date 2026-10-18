namespace HomeDaemon.Application.Budget;

public sealed record GetBudgetIncomesQuery(Guid HouseholdId, int Year);
