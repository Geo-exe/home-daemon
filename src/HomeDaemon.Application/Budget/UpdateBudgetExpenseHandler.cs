using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class UpdateBudgetExpenseHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetExpenseDto> HandleAsync(UpdateBudgetExpenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        BudgetGuards.RequirePositive(command.AmountMinor, "expense");

        var expense = await db.BudgetExpenses
            .FirstOrDefaultAsync(item => item.Id == command.ExpenseId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Expense '{command.ExpenseId}' was not found.");

        var category = await BudgetGuards.RequireCategoryAsync(db, command.HouseholdId, command.CategoryId, cancellationToken).ConfigureAwait(false);
        var subcategory = command.SubcategoryId is Guid subcategoryId
            ? await BudgetGuards.RequireSubcategoryAsync(db, command.HouseholdId, command.CategoryId, subcategoryId, cancellationToken).ConfigureAwait(false)
            : null;
        var member = await BudgetGuards.RequireParticipantAsync(db, command.HouseholdId, command.PaidByMemberId, cancellationToken).ConfigureAwait(false);

        expense.Date = command.Date;
        expense.AmountMinor = command.AmountMinor;
        expense.CategoryId = category.Id;
        expense.SubcategoryId = subcategory?.Id;
        expense.PaidByMemberId = member.Id;
        expense.Description = CreateBudgetExpenseHandler.NullIfWhiteSpace(command.Description);
        expense.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return BudgetMappings.ToDto(expense, category.Name, subcategory?.Name, member.Name);
    }
}
