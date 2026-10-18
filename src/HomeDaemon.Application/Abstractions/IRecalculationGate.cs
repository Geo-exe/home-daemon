namespace HomeDaemon.Application.Abstractions;

/// <summary>
/// Process-wide mutual exclusion for scheduling recalculations so the nightly job, the
/// manual endpoint and a completion-triggered recalculation never run concurrently.
/// </summary>
public interface IRecalculationGate
{
    /// <summary>
    /// Waits until the gate is free, then returns a handle that releases it when disposed.
    /// </summary>
    Task<IDisposable> AcquireAsync(CancellationToken cancellationToken);
}
