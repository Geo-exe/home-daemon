namespace HomeDaemon.Api.Endpoints;

public sealed record CreateBudgetExpenseRequest(
    DateOnly Date,
    long AmountMinor,
    Guid CategoryId,
    Guid? SubcategoryId,
    Guid PaidByMemberId,
    string? Description);
