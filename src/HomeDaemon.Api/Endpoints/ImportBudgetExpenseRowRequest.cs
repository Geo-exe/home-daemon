namespace HomeDaemon.Api.Endpoints;

/// <summary>One already-normalized expense row; amounts are minor units and dates are ISO.</summary>
public sealed record ImportBudgetExpenseRowRequest(
    int Line,
    DateOnly Date,
    long AmountMinor,
    string Category,
    string? Subcategory,
    Guid PaidByMemberId,
    string? Description);
