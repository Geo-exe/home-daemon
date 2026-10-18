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
    public async Task<ChoreCompletionResult> HandleAsync(RecordHomeAssistantCompletionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var link = await db.TodoItemLinks
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.TodoListEntityId == command.TodoListEntityId && item.ItemUid == command.ItemUid, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException("No chore is associated with the provided to-do item.");

        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        var today = HouseholdClock.Today(household, timeProvider);
        var idempotencyKey = string.IsNullOrWhiteSpace(command.IdempotencyKey)
            ? $"ha:{command.TodoListEntityId}:{command.ItemUid}:{today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : command.IdempotencyKey;

        return await completion
            .CompleteAsync(link.ChoreDefinitionId, today, command.CompletedByMemberId, CompletionSource.HomeAssistant, idempotencyKey, cancellationToken)
            .ConfigureAwait(false);
    }
}
