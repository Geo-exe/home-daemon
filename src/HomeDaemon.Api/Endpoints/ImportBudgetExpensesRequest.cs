namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// A batch of already-normalized rows plus the explicit category resolutions chosen in the
/// import preview. Amounts are minor units and dates are ISO calendar dates.
/// </summary>
public sealed record ImportBudgetExpensesRequest(
    IReadOnlyList<ImportBudgetExpenseRowRequest>? Rows,
    IReadOnlyList<ImportCategoryMappingRequest>? Categories,
    IReadOnlyList<ImportSubcategoryMappingRequest>? Subcategories);
