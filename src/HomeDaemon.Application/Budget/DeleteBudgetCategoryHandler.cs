using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class DeleteBudgetCategoryHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteBudgetCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var category = await db.BudgetCategories
            .FirstOrDefaultAsync(item => item.Id == command.CategoryId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Category '{command.CategoryId}' was not found.");

        if (await db.BudgetExpenses.AnyAsync(expense => expense.CategoryId == category.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new EntityConflictException("The category is still used by expenses.");
        }

        var subcategories = await db.BudgetSubcategories
            .Where(subcategory => subcategory.CategoryId == category.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (subcategories.Count > 0)
        {
            var subcategoryIds = subcategories.Select(subcategory => subcategory.Id).ToList();
            var referenced = await db.BudgetExpenses
                .AnyAsync(expense => expense.SubcategoryId != null && subcategoryIds.Contains(expense.SubcategoryId.Value), cancellationToken)
                .ConfigureAwait(false);

            if (referenced)
            {
                throw new EntityConflictException("The category is still used by expenses.");
            }

            db.BudgetSubcategories.RemoveRange(subcategories);
        }

        db.BudgetCategories.Remove(category);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
