using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class GetBudgetExpenseHandler(IApplicationDbContext db)
{
    public async Task<BudgetExpenseDto> HandleAsync(GetBudgetExpenseQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var expense = await db.BudgetExpenses
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == query.ExpenseId && item.HouseholdId == query.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Expense '{query.ExpenseId}' was not found.");

        var categoryName = await db.BudgetCategories
            .AsNoTracking()
            .Where(category => category.Id == expense.CategoryId)
            .Select(category => category.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        var subcategoryName = expense.SubcategoryId is Guid subcategoryId
            ? await db.BudgetSubcategories
                .AsNoTracking()
                .Where(subcategory => subcategory.Id == subcategoryId)
                .Select(subcategory => subcategory.Name)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false)
            : null;

        var paidByName = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.Id == expense.PaidByMemberId)
            .Select(member => member.Name)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        return BudgetMappings.ToDto(expense, categoryName, subcategoryName, paidByName);
    }
}
