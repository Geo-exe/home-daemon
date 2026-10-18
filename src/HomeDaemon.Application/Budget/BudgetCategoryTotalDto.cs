namespace HomeDaemon.Application.Budget;

public sealed record BudgetCategoryTotalDto(Guid CategoryId, string Name, long AmountMinor);
