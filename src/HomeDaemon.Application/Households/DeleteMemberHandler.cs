using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

/// <summary>
/// Deletes a member only when nothing references them: not an active or deleted chore, not a
/// single completion in the history, and not a budget income, expense or transfer. History is
/// immutable, so a member who has ever been used is kept forever; the caller should stop
/// scheduling to them instead.
/// </summary>
public sealed class DeleteMemberHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteMemberCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var member = await db.HouseholdMembers
            .FirstOrDefaultAsync(item => item.Id == command.MemberId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Member '{command.MemberId}' was not found.");

        var memberId = member.Id;

        var referencedByChore = await db.Chores
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(
                chore => chore.FixedAssigneeMemberId == memberId
                    || chore.PlannedMemberId == memberId
                    || chore.LastCompletedByMemberId == memberId,
                cancellationToken)
            .ConfigureAwait(false);

        var referencedByHistory = await db.CompletionHistory
            .AsNoTracking()
            .AnyAsync(history => history.CompletedByMemberId == memberId, cancellationToken)
            .ConfigureAwait(false);

        var referencedByBudget = await db.BudgetIncomes
            .AsNoTracking()
            .AnyAsync(income => income.MemberId == memberId, cancellationToken)
            .ConfigureAwait(false)
            || await db.BudgetExpenses
                .AsNoTracking()
                .AnyAsync(expense => expense.PaidByMemberId == memberId, cancellationToken)
                .ConfigureAwait(false)
            || await db.BudgetTransfers
                .AsNoTracking()
                .AnyAsync(transfer => transfer.FromMemberId == memberId || transfer.ToMemberId == memberId, cancellationToken)
                .ConfigureAwait(false);

        if (referencedByChore || referencedByHistory || referencedByBudget)
        {
            throw new EntityConflictException($"Member '{memberId}' is still referenced by chores, history or the budget.");
        }

        db.HouseholdMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
