namespace HomeDaemon.Application.Budget;

public sealed record BudgetSubcategoryDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    int SortOrder);
