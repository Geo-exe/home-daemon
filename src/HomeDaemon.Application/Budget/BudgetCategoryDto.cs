namespace HomeDaemon.Application.Budget;

public sealed record BudgetCategoryDto(
    Guid Id,
    string Name,
    int SortOrder,
    IReadOnlyList<BudgetSubcategoryDto> Subcategories);
