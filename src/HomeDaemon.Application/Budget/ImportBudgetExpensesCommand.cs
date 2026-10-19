namespace HomeDaemon.Application.Budget;

public sealed record ImportBudgetExpensesCommand(
    Guid HouseholdId,
    IReadOnlyList<ImportBudgetExpenseRow> Rows,
    IReadOnlyList<ImportCategoryMapping> Categories,
    IReadOnlyList<ImportSubcategoryMapping> Subcategories);
