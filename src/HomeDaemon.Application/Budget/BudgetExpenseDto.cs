namespace HomeDaemon.Application.Budget;

public sealed record BudgetExpenseDto(
    Guid Id,
    DateOnly Date,
    long AmountMinor,
    Guid CategoryId,
    string CategoryName,
    Guid? SubcategoryId,
    string? SubcategoryName,
    Guid PaidByMemberId,
    string PaidByMemberName,
    string? Description);
