namespace HomeDaemon.Domain.Budget;

/// <summary>The full balance of a settlement year: the inputs plus the per-member result.</summary>
public sealed record BudgetBalance(
    long TotalIncomeMinor,
    long TotalExpensesMinor,
    bool ProportionAvailable,
    IReadOnlyList<BudgetMemberBalance> Members);
