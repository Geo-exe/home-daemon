using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class DeleteBudgetSubcategoryHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteBudgetSubcategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var subcategory = await db.BudgetSubcategories
            .FirstOrDefaultAsync(item => item.Id == command.SubcategoryId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Subcategory '{command.SubcategoryId}' was not found.");

        if (await db.BudgetExpenses.AnyAsync(expense => expense.SubcategoryId == subcategory.Id, cancellationToken).ConfigureAwait(false))
        {
            throw new EntityConflictException("The subcategory is still used by expenses.");
        }

        db.BudgetSubcategories.Remove(subcategory);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
