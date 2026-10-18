using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class UpdateBudgetTransferHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetTransferDto> HandleAsync(UpdateBudgetTransferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var transfer = await db.BudgetTransfers
            .FirstOrDefaultAsync(item => item.Id == command.TransferId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Transfer '{command.TransferId}' was not found.");

        var (fromMember, toMember) = await CreateBudgetTransferHandler.ValidateAsync(
            db,
            command.HouseholdId,
            command.SettlementYear,
            command.AmountMinor,
            command.FromMemberId,
            command.ToMemberId,
            cancellationToken).ConfigureAwait(false);

        transfer.Date = command.Date;
        transfer.SettlementYear = command.SettlementYear;
        transfer.AmountMinor = command.AmountMinor;
        transfer.FromMemberId = fromMember.Id;
        transfer.ToMemberId = toMember.Id;
        transfer.Description = CreateBudgetTransferHandler.NullIfWhiteSpace(command.Description);
        transfer.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return BudgetMappings.ToDto(transfer, fromMember.Name, toMember.Name);
    }
}
