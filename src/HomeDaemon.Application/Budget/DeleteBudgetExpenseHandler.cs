using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class DeleteBudgetExpenseHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteBudgetExpenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var expense = await db.BudgetExpenses
            .FirstOrDefaultAsync(item => item.Id == command.ExpenseId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Expense '{command.ExpenseId}' was not found.");

        db.BudgetExpenses.Remove(expense);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
