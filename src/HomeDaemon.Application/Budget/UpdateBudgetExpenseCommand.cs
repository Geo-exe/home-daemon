namespace HomeDaemon.Application.Budget;

public sealed record UpdateBudgetExpenseCommand(
    Guid HouseholdId,
    Guid ExpenseId,
    DateOnly Date,
    long AmountMinor,
    Guid CategoryId,
    Guid? SubcategoryId,
    Guid PaidByMemberId,
    string? Description);
