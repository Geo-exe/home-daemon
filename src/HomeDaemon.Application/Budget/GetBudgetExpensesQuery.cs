namespace HomeDaemon.Application.Budget;

/// <summary>
/// Paged expense list. All filters are optional; the end date is inclusive. When the year is
/// set it is applied together with any explicit date range.
/// </summary>
public sealed record GetBudgetExpensesQuery(
    Guid HouseholdId,
    int? Year,
    DateOnly? From,
    DateOnly? To,
    Guid? CategoryId,
    Guid? SubcategoryId,
    Guid? MemberId,
    string? Cursor,
    int? PageSize);
