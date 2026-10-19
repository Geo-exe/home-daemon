namespace HomeDaemon.Application.Budget;

/// <summary>
/// Returns the most recent distinct transfer directions. The year is deliberately not part of
/// the query: suggestions should stay stable while the list is filtered by settlement year.
/// </summary>
public sealed record GetBudgetTransferSuggestionsQuery(Guid HouseholdId, int? Limit);
