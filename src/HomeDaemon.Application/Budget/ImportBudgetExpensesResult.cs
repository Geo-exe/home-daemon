namespace HomeDaemon.Application.Budget;

/// <summary>
/// <paramref name="Imported"/> counts new expenses; <paramref name="Skipped"/> counts rows that
/// already existed with the same date, amount, category, payer and description, so importing
/// the same file twice is safe.
/// </summary>
public sealed record ImportBudgetExpensesResult(int Imported, int Skipped);
