namespace HomeDaemon.Api.Endpoints;

/// <summary>Creates or replaces the single income row for a person, year and month.</summary>
public sealed record UpsertBudgetIncomeRequest(Guid MemberId, int Year, int Month, long AmountMinor);
