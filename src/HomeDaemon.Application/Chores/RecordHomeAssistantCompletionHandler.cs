using System.Globalization;
using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

public sealed class RecordHomeAssistantCompletionHandler(
    IApplicationDbContext db,
    ChoreCompletionService completion,
    TimeProvider timeProvider)
{
    /// <summary>
    /// Records a completion reported by Home Assistant. Returns <see langword="null"/> when the
    /// item is not one HomeDaemon placed: an unmapped to-do list is a normal event (the user may
    /// own other lists), not an error the integration should retry.
    /// </summary>
    public async Task<ChoreCompletionResult?> HandleAsync(RecordHomeAssistantCompletionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var link = await db.TodoItemLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TodoListEntityId == command.TodoListEntityId && item.ItemUid == command.ItemUid, cancellationToken)
            .ConfigureAwait(false);

        if (link is null)
        {
            return null;
        }

        // Lists belong to members, so the list itself identifies who completed the chore when
        // Home Assistant does not send an explicit member id.
        var memberId = command.CompletedByMemberId
            ?? await db.HouseholdMembers
                .AsNoTracking()
                .Where(member => member.HouseholdId == link.HouseholdId
                    && member.TodoListEntityId == command.TodoListEntityId)
                .Select(member => (Guid?)member.Id)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        var today = HouseholdClock.Today(household, timeProvider);
        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? $"ha:{command.TodoListEntityId}:{command.ItemUid}:{today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : command.IdempotencyKey;

        return await completion
            .CompleteAsync(link.ChoreDefinitionId, today, memberId, CompletionSource.HomeAssistant, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
    }
}
