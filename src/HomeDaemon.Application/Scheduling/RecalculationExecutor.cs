using System.Globalization;
using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Sync;
using HomeDaemon.Domain.Chores;
using HomeDaemon.Domain.Households;
using HomeDaemon.Domain.Scheduling;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Runs a single recalculation: build availability, place the chores due inside the rolling
/// horizon, persist the plan and queue Home Assistant reconciliation. The run row records
/// counts and diagnostics so failures stay observable.
/// </summary>
public sealed class RecalculationExecutor(
    IApplicationDbContext db,
    AvailabilityMatrixBuilder availabilityBuilder,
    ChoreSyncService sync,
    IRecalculationGate recalculationGate,
    IOptions<SchedulingOptions> schedulingOptions,
    TimeProvider timeProvider,
    ILogger<RecalculationExecutor> logger)
{
    public async Task ExecuteAsync(Guid scheduleRunId, CancellationToken cancellationToken)
    {
        // One recalculation at a time across the nightly job, the manual endpoint and
        // completion-driven runs.
        using var lease = await recalculationGate.AcquireAsync(cancellationToken).ConfigureAwait(false);

        var run = await db.ScheduleRuns
            .FirstOrDefaultAsync(item => item.Id == scheduleRunId, cancellationToken)
            .ConfigureAwait(false);

        if (run is null)
        {
            return;
        }

        try
        {
            await ExecuteCoreAsync(run, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            SchedulingLog.RecalculationFailed(logger, scheduleRunId, ex);
            run.Status = ScheduleRunStatus.Failed;
            run.CompletedAtUtc = timeProvider.GetUtcNow();
            run.ErrorMessage = ex.Message;
            await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
        }
    }

    private async Task ExecuteCoreAsync(ScheduleRun run, CancellationToken cancellationToken)
    {
        var household = await db.Households
            .FirstOrDefaultAsync(item => item.Id == run.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Household '{run.HouseholdId}' was not found.");

        var today = HouseholdClock.Today(household, timeProvider);
        var window = PlanningWindow.For(household, today);

        var members = await db.HouseholdMembers
            .Where(member => member.HouseholdId == household.Id && member.IsAssignable)
            .OrderBy(member => member.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var calendarsByMember = await MemberCalendarReader.GetByMemberAsync(db, household.Id, cancellationToken).ConfigureAwait(false);
        var matrix = await availabilityBuilder
            .BuildAsync(household, members, calendarsByMember, window.FromInclusive, window.ToExclusive, cancellationToken)
            .ConfigureAwait(false);

        var allChores = await db.Chores
            .Where(chore => chore.HouseholdId == household.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeChores = allChores.Where(chore => chore.IsActive).ToList();
        var candidates = BuildCandidates(activeChores, members, window, today);
        var evaluatedIds = candidates.Select(candidate => candidate.ChoreId).ToHashSet();

        var result = SchedulingEngine.Schedule(new SchedulingInput(
            window.FromInclusive,
            window.ToInclusive,
            candidates,
            BuildFreeMinutes(matrix),
            schedulingOptions.Value));

        ApplyResult(allChores, result);
        await EnqueueSyncAsync(household, allChores, evaluatedIds, cancellationToken).ConfigureAwait(false);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        CompleteRun(run, result, matrix, window);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    private static Dictionary<(DateOnly Date, Guid MemberId), int> BuildFreeMinutes(AvailabilityMatrix matrix)
    {
        var freeMinutes = new Dictionary<(DateOnly Date, Guid MemberId), int>();
        foreach (var entry in matrix.Availability)
        {
            freeMinutes[entry.Key] = entry.Value.FreeMinutes;
        }

        return freeMinutes;
    }

    private async Task EnqueueSyncAsync(
        Household household,
        IReadOnlyList<ChoreDefinition> allChores,
        HashSet<Guid> evaluatedIds,
        CancellationToken cancellationToken)
    {
        var linkedChoreIds = await db.TodoItemLinks
            .Where(link => link.HouseholdId == household.Id)
            .Select(link => link.ChoreDefinitionId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var syncSet = allChores
            .Where(chore => evaluatedIds.Contains(chore.Id) || linkedChoreIds.Contains(chore.Id))
            .ToList();

        await sync.EnqueueAsync(household.Id, syncSet, cancellationToken).ConfigureAwait(false);
    }

    private void CompleteRun(ScheduleRun run, SchedulingResult result, AvailabilityMatrix matrix, PlanningWindow window)
    {
        var from = window.FromInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var to = window.ToInclusive.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        run.Status = ScheduleRunStatus.Succeeded;
        run.CompletedAtUtc = timeProvider.GetUtcNow();
        run.ScheduledCount = result.Assignments.Count;
        run.UnscheduledCount = result.Unscheduled.Count;
        run.Summary = $"{result.Assignments.Count} scheduled, {result.Unscheduled.Count} unscheduled from {from} to {to}.";
        run.Diagnostics = matrix.Diagnostics.Count == 0 ? null : string.Join(" | ", matrix.Diagnostics);
    }

    private static List<ChoreCandidate> BuildCandidates(
        IReadOnlyList<ChoreDefinition> activeChores,
        IReadOnlyList<HouseholdMember> members,
        PlanningWindow window,
        DateOnly today)
    {
        var candidates = new List<ChoreCandidate>();
        foreach (var chore in activeChores)
        {
            // A manual override persists until completion or an explicit unlock, even when it
            // sits outside the rolling window.
            var isLocked = chore.IsLocked && chore.NextDueDate is not null && chore.PlannedMemberId is not null;

            var due = chore.NextOccurrenceDate;
            var dueWithinWindow = due is DateOnly dueDate && window.Contains(dueDate);
            var isOverdue = due is DateOnly overdueDate && overdueDate < window.FromInclusive;

            if (!isLocked && !dueWithinWindow && !isOverdue)
            {
                continue;
            }

            candidates.Add(new ChoreCandidate(
                chore.Id,
                chore.RoomId,
                chore.EstimatedMinutes,
                due ?? chore.NextDueDate ?? today,
                isLocked,
                isLocked ? chore.NextDueDate : null,
                isLocked ? chore.PlannedMemberId : null,
                chore.PreferSameRoomGrouping,
                MemberAssignmentPolicyResolver.Resolve(chore, members),
                chore.LastCompletedDate));
        }

        return candidates;
    }

    private void ApplyResult(
        IReadOnlyList<ChoreDefinition> allChores,
        SchedulingResult result)
    {
        var assignments = result.Assignments.ToDictionary(assignment => assignment.ChoreId);
        var unscheduled = result.Unscheduled.ToDictionary(item => item.ChoreId);
        var now = timeProvider.GetUtcNow();

        foreach (var chore in allChores)
        {
            if (!chore.IsActive)
            {
                chore.ScheduleState = ChoreScheduleState.Paused;
                ClearPlan(chore);
            }
            else if (assignments.TryGetValue(chore.Id, out var assignment))
            {
                chore.NextDueDate = assignment.Date;
                chore.PlannedMemberId = assignment.MemberId;
                chore.ScheduleState = ChoreScheduleState.Scheduled;
                chore.UnscheduledReason = null;
            }
            else if (unscheduled.TryGetValue(chore.Id, out var item))
            {
                ClearPlan(chore);
                chore.ScheduleState = ChoreScheduleState.Unscheduled;
                chore.UnscheduledReason = item.Reason;
            }
            else if (chore.IsLocked && chore.NextDueDate is not null && chore.PlannedMemberId is not null)
            {
                // Never discard a manual override that is out of the scheduling window.
                chore.ScheduleState = ChoreScheduleState.Scheduled;
                chore.UnscheduledReason = null;
            }
            else
            {
                ClearPlan(chore);
            }

            chore.UpdatedAtUtc = now;
        }
    }

    private static void ClearPlan(ChoreDefinition chore)
    {
        chore.NextDueDate = null;
        chore.PlannedMemberId = null;
        if (chore.IsActive)
        {
            chore.ScheduleState = ChoreScheduleState.Unplanned;
        }

        chore.UnscheduledReason = null;
    }
}
