using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Scheduling;

namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Creates a schedule run record and enqueues it. The same case is used by the nightly job,
/// the manual endpoint and any future automation; no separate algorithm variant exists.
/// </summary>
public sealed class RecalculateScheduleCommandHandler(
    IApplicationDbContext db,
    IScheduleRunDispatcher queue,
    TimeProvider timeProvider)
{
    public async Task<Guid> HandleAsync(RecalculateScheduleCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        var run = new ScheduleRun
        {
            HouseholdId = household.Id,
            Trigger = command.Trigger,
            Status = ScheduleRunStatus.Running,
            StartedAtUtc = timeProvider.GetUtcNow(),
        };

        db.ScheduleRuns.Add(run);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        await queue.EnqueueAsync(run.Id, cancellationToken).ConfigureAwait(false);
        return run.Id;
    }
}
