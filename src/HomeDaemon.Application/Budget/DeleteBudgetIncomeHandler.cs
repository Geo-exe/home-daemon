using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class DeleteBudgetIncomeHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteBudgetIncomeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var income = await db.BudgetIncomes
            .FirstOrDefaultAsync(item => item.Id == command.IncomeId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Income '{command.IncomeId}' was not found.");

        db.BudgetIncomes.Remove(income);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
