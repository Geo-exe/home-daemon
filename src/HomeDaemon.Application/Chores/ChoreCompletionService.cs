using System.Globalization;
using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Recurrence;
using HomeDaemon.Application.Scheduling;
using HomeDaemon.Application.Sync;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// Shared completion flow used by both the manual API and the Home Assistant callback.
/// It records a single immutable completion, computes the next theoretical occurrence and
/// provisionally re-places the chore, then queues the Home Assistant reconciliation.
/// </summary>
public sealed class ChoreCompletionService(
    IApplicationDbContext db,
    IChoreRecurrenceService recurrence,
    ChoreSyncService sync,
    IRecalculationGate recalculationGate,
    TimeProvider timeProvider)
{
    public async Task<ChoreCompletionResult> CompleteAsync(
        Guid choreId,
        DateOnly? completedDate,
        Guid? completedByMemberId,
        CompletionSource source,
        string? idempotencyKey,
        CancellationToken cancellationToken)
    {
        // Completions write the same chore rows the scheduler writes, so they take the
        // recalculation lock to avoid interleaving with a running recalculation.
        using var lease = await recalculationGate.AcquireAsync(cancellationToken).ConfigureAwait(false);

        var chore = await db.Chores
            .FirstOrDefaultAsync(item => item.Id == choreId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Chore '{choreId}' was not found.");

        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        var effectiveDate = completedDate ?? HouseholdClock.Today(household, timeProvider);
        var normalizedKey = NormalizeKey(idempotencyKey, source, chore.Id, effectiveDate);

        if (await IsDuplicateAsync(choreId, normalizedKey, cancellationToken).ConfigureAwait(false))
        {
            return new ChoreCompletionResult(chore.Id, true);
        }

        var now = timeProvider.GetUtcNow();
        db.CompletionHistory.Add(new CompletionHistory
        {
            HouseholdId = chore.HouseholdId,
            ChoreDefinitionId = chore.Id,
            CompletedDate = effectiveDate,
            RecordedAtUtc = now,
            Source = source,
            IdempotencyKey = normalizedKey,
            CompletedByMemberId = completedByMemberId,
        });

        chore.LastCompletedDate = effectiveDate;
        chore.LastCompletedByMemberId = completedByMemberId ?? chore.PlannedMemberId ?? chore.LastCompletedByMemberId;
        chore.NextOccurrenceDate = recurrence.GetNextAfterCompletion(chore, effectiveDate);
        chore.IsLocked = false;
        chore.NextDueDate = null;
        chore.PlannedMemberId = null;
        chore.UnscheduledReason = null;
        chore.ScheduleState = ChoreScheduleState.Unplanned;
        chore.UpdatedAtUtc = now;

        await PlaceProvisionalAsync(chore, household, cancellationToken).ConfigureAwait(false);
        await sync.EnqueueAsync(chore.HouseholdId, [chore], cancellationToken).ConfigureAwait(false);

        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException)
        {
            // A concurrent request with the same idempotency key won the race.
            if (await IsDuplicateAsync(chore.Id, normalizedKey, cancellationToken).ConfigureAwait(false))
            {
                return new ChoreCompletionResult(chore.Id, true);
            }

            throw;
        }

        return new ChoreCompletionResult(chore.Id, false);
    }

    private static string NormalizeKey(string? idempotencyKey, CompletionSource source, Guid choreId, DateOnly completedDate) =>
        string.IsNullOrWhiteSpace(idempotencyKey)
            ? $"{source.ToString().ToUpperInvariant()}:{choreId}:{completedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}"
            : idempotencyKey.Trim();

    private async Task<bool> IsDuplicateAsync(Guid choreId, string idempotencyKey, CancellationToken cancellationToken) =>
        await db.CompletionHistory
            .AnyAsync(history => history.ChoreDefinitionId == choreId && history.IdempotencyKey == idempotencyKey, cancellationToken)
            .ConfigureAwait(false);

    private async Task PlaceProvisionalAsync(ChoreDefinition chore, Household household, CancellationToken cancellationToken)
    {
        if (!chore.IsActive || chore.NextOccurrenceDate is not DateOnly occurrence)
        {
            chore.ScheduleState = ChoreScheduleState.Unplanned;
            return;
        }

        var window = PlanningWindow.For(household, HouseholdClock.Today(household, timeProvider));
        if (!window.Contains(occurrence))
        {
            chore.ScheduleState = ChoreScheduleState.Unplanned;
            return;
        }

        var members = await db.HouseholdMembers
            .Where(member => member.HouseholdId == chore.HouseholdId)
            .OrderBy(member => member.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var allowed = MemberAssignmentPolicyResolver.Resolve(chore, members);
        if (allowed.Count == 0)
        {
            chore.ScheduleState = ChoreScheduleState.Unscheduled;
            chore.UnscheduledReason = "No assignable member matches the assignment policy.";
            return;
        }

        var chosen = allowed.FirstOrDefault(id => id != chore.LastCompletedByMemberId);
        if (chosen == Guid.Empty)
        {
            chosen = allowed[0];
        }

        chore.NextDueDate = occurrence;
        chore.PlannedMemberId = chosen;
        chore.ScheduleState = ChoreScheduleState.Scheduled;
    }
}
