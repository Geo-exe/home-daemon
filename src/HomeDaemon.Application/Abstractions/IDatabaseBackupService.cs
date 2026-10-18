namespace HomeDaemon.Application.Abstractions;

/// <summary>
/// Creates a consistent SQLite backup using the provider's online backup API rather than
/// copying the live file, which would ignore the write-ahead log.
/// </summary>
public interface IDatabaseBackupService
{
    Task<DatabaseBackupResult> CreateBackupAsync(CancellationToken cancellationToken);
}
