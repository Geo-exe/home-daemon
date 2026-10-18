namespace HomeDaemon.Application.Budget;

public sealed record BudgetMemberIncomesDto(
    Guid MemberId,
    string Name,
    IReadOnlyList<BudgetIncomeMonthDto> Months);
