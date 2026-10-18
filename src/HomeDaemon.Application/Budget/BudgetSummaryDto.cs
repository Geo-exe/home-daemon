namespace HomeDaemon.Application.Budget;

public sealed record BudgetSummaryDto(
    int Year,
    string Currency,
    long TotalIncomeMinor,
    long TotalExpensesMinor,
    bool ProportionAvailable,
    bool IncomeComplete,
    IReadOnlyList<BudgetMemberSummaryDto> Members,
    IReadOnlyList<BudgetCategoryTotalDto> ByCategory);
