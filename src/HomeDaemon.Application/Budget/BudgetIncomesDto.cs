namespace HomeDaemon.Application.Budget;

public sealed record BudgetIncomesDto(
    int Year,
    string Currency,
    IReadOnlyList<BudgetMemberIncomesDto> Members);
