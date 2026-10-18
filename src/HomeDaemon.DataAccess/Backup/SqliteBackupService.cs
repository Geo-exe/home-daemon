using System.Globalization;
using HomeDaemon.Application.Abstractions;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace HomeDaemon.DataAccess.Backup;

/// <summary>
/// Creates a consistent SQLite backup with the provider's online backup API, validates its
/// integrity and promotes it atomically. A failed backup never deletes older good backups.
/// </summary>
public sealed class SqliteBackupService(
    IConfiguration configuration,
    IOptions<BackupOptions> options,
    TimeProvider timeProvider,
    ILogger<SqliteBackupService> logger) : IDatabaseBackupService
{
    private const string DefaultFileName = "home-daemon.db";

    public async Task<DatabaseBackupResult> CreateBackupAsync(CancellationToken cancellationToken)
    {
        var connectionString = configuration.GetConnectionString("HomeDaemon") ?? $"Data Source={DefaultFileName}";
        var directory = string.IsNullOrWhiteSpace(options.Value.Directory)
            ? Path.Combine(AppContext.BaseDirectory, "backups")
            : options.Value.Directory;

        Directory.CreateDirectory(directory);

        var createdAt = timeProvider.GetUtcNow();
        var fileName = $"home-daemon-{createdAt.ToString("yyyyMMdd-HHmmss", CultureInfo.InvariantCulture)}.db";
        var destinationPath = Path.Combine(directory, fileName);
        var temporaryPath = destinationPath + ".tmp";

        Backup(connectionString, temporaryPath);
        await ValidateAsync(temporaryPath, cancellationToken).ConfigureAwait(false);

        File.Move(temporaryPath, destinationPath, overwrite: true);
        var size = new FileInfo(destinationPath).Length;
        BackupLog.BackupWritten(logger, destinationPath, size);

        ApplyRetention(directory, options.Value.RetentionCount);
        return new DatabaseBackupResult(destinationPath, createdAt, size);
    }

    private static void Backup(string connectionString, string temporaryPath)
    {
        using var sourceConnection = new SqliteConnection(connectionString);
        using var destinationConnection = new SqliteConnection($"Data Source={temporaryPath}");
        sourceConnection.Open();
        destinationConnection.Open();
        sourceConnection.BackupDatabase(destinationConnection);
    }

    private static async Task ValidateAsync(string temporaryPath, CancellationToken cancellationToken)
    {
        using var connection = new SqliteConnection($"Data Source={temporaryPath}");
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);

        using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) as string;

        if (!string.Equals(result, "ok", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"SQLite backup integrity check failed: {result}");
        }
    }

    private void ApplyRetention(string directory, int retentionCount)
    {
        if (retentionCount <= 0)
        {
            return;
        }

        var stale = Directory
            .EnumerateFiles(directory, "home-daemon-*.db")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Skip(retentionCount)
            .ToList();

        foreach (var file in stale)
        {
            BackupLog.RemovingStaleBackup(logger, file);
            File.Delete(file);
        }
    }
}
