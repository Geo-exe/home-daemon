namespace HomeDaemon.Application.Budget;

/// <summary>One normalized row of a CSV import. Parsing and column mapping happen at the edge.</summary>
public sealed record ImportBudgetExpenseRow(
    int Line,
    DateOnly Date,
    long AmountMinor,
    string Category,
    string? Subcategory,
    Guid PaidByMemberId,
    string? Description);
