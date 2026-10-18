namespace HomeDaemon.Application.Budget;

public sealed record GetBudgetSummaryQuery(Guid HouseholdId, int Year);
