using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

/// <summary>
/// Deletes a member only when nothing references them: not an active or deleted chore, and not
/// a single completion in the history. History is immutable, so a member who has ever
/// completed a chore is kept forever; the caller should stop scheduling to them instead.
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

        if (referencedByChore || referencedByHistory)
        {
            throw new EntityConflictException($"Member '{memberId}' is still referenced by chores or completion history.");
        }

        db.HouseholdMembers.Remove(member);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
