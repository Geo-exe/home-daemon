namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// In-process queue of pending schedule runs. Decoupling the HTTP request from the work
/// lets the manual endpoint answer 202 while a hosted service performs the recalculation.
/// </summary>
public interface IScheduleRunDispatcher
{
    ValueTask EnqueueAsync(Guid scheduleRunId, CancellationToken cancellationToken);

    /// <summary>Waits for the next run id, or returns null when the queue is empty after signalling.</summary>
    Task<Guid?> DequeueAsync(CancellationToken cancellationToken);
}
