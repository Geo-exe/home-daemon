using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class DeleteBudgetTransferHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteBudgetTransferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var transfer = await db.BudgetTransfers
            .FirstOrDefaultAsync(item => item.Id == command.TransferId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Transfer '{command.TransferId}' was not found.");

        db.BudgetTransfers.Remove(transfer);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
