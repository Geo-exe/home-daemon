namespace HomeDaemon.Application.Sync;

public sealed class TodoSyncOptions
{
    /// <summary>Maximum number of outbox entries processed in one pass.</summary>
    public int BatchSize { get; set; } = 50;

    /// <summary>Number of transient failures tolerated before an entry is marked failed.</summary>
    public int MaxAttempts { get; set; } = 5;

    /// <summary>How often the sync worker drains the outbox, in seconds.</summary>
    public int SyncIntervalSeconds { get; set; } = 30;
}
