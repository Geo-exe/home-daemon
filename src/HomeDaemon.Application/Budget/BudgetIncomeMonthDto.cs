namespace HomeDaemon.Application.Budget;

/// <summary>
/// One cell of the monthly income grid. A null amount means the month was never entered; a
/// zero means the income was genuinely nothing.
/// </summary>
public sealed record BudgetIncomeMonthDto(int Month, long? AmountMinor, Guid? IncomeId);
