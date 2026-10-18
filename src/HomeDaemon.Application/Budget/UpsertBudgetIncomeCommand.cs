namespace HomeDaemon.Application.Budget;

/// <summary>Creates or replaces the single income row for a person, year and month.</summary>
public sealed record UpsertBudgetIncomeCommand(Guid HouseholdId, Guid MemberId, int Year, int Month, long AmountMinor);
