namespace HomeDaemon.Domain.Budget;

/// <summary>An expense reduced to the payer and the amount, for the balance calculation.</summary>
public sealed record BudgetExpenseLine(Guid PaidByMemberId, long AmountMinor);
