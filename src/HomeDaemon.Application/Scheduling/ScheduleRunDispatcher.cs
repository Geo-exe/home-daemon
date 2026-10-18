using System.Collections.Concurrent;

namespace HomeDaemon.Application.Scheduling;

public sealed class ScheduleRunDispatcher : IScheduleRunDispatcher, IDisposable
{
    private readonly ConcurrentQueue<Guid> _queue = new();
    private readonly SemaphoreSlim _signal = new(0, int.MaxValue);

    public ValueTask EnqueueAsync(Guid scheduleRunId, CancellationToken cancellationToken)
    {
        _queue.Enqueue(scheduleRunId);
        _signal.Release();
        return ValueTask.CompletedTask;
    }

    public async Task<Guid?> DequeueAsync(CancellationToken cancellationToken)
    {
        await _signal.WaitAsync(cancellationToken).ConfigureAwait(false);
        return _queue.TryDequeue(out var scheduleRunId) ? scheduleRunId : null;
    }

    public void Dispose()
    {
        _signal.Dispose();
        GC.SuppressFinalize(this);
    }
}
