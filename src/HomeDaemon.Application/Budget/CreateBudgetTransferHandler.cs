using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Budget;

public sealed class CreateBudgetTransferHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetTransferDto> HandleAsync(CreateBudgetTransferCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var (fromMember, toMember) = await ValidateAsync(
            db,
            command.HouseholdId,
            command.SettlementYear,
            command.AmountMinor,
            command.FromMemberId,
            command.ToMemberId,
            cancellationToken).ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var transfer = new BudgetTransfer
        {
            HouseholdId = command.HouseholdId,
            Date = command.Date,
            SettlementYear = command.SettlementYear,
            AmountMinor = command.AmountMinor,
            FromMemberId = fromMember.Id,
            ToMemberId = toMember.Id,
            Description = NullIfWhiteSpace(command.Description),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.BudgetTransfers.Add(transfer);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BudgetMappings.ToDto(transfer, fromMember.Name, toMember.Name);
    }

    internal static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    internal static async Task<(HouseholdMember From, HouseholdMember To)> ValidateAsync(
        IApplicationDbContext db,
        Guid householdId,
        int settlementYear,
        long amountMinor,
        Guid fromMemberId,
        Guid toMemberId,
        CancellationToken cancellationToken)
    {
        BudgetGuards.RequirePositive(amountMinor, "transfer");
        BudgetGuards.RequireYear(settlementYear);

        if (fromMemberId == toMemberId)
        {
            throw new ApplicationValidationException("The sender and the receiver must be different people.");
        }

        var fromMember = await BudgetGuards.RequireParticipantAsync(db, householdId, fromMemberId, cancellationToken).ConfigureAwait(false);
        var toMember = await BudgetGuards.RequireParticipantAsync(db, householdId, toMemberId, cancellationToken).ConfigureAwait(false);
        return (fromMember, toMember);
    }
}
