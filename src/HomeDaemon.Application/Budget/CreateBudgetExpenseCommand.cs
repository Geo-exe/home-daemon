namespace HomeDaemon.Application.Budget;

public sealed record CreateBudgetExpenseCommand(
    Guid HouseholdId,
    DateOnly Date,
    long AmountMinor,
    Guid CategoryId,
    Guid? SubcategoryId,
    Guid PaidByMemberId,
    string? Description);
