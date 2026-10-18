using Microsoft.Extensions.Logging;

namespace HomeDaemon.DataAccess.Backup;

/// <summary>Source-generated, allocation-free log messages for the backup service.</summary>
internal static partial class BackupLog
{
    [LoggerMessage(EventId = 1001, Level = LogLevel.Information, Message = "SQLite backup written to {BackupPath} ({SizeBytes} bytes).")]
    public static partial void BackupWritten(ILogger logger, string backupPath, long sizeBytes);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Information, Message = "Removing stale SQLite backup {BackupPath}.")]
    public static partial void RemovingStaleBackup(ILogger logger, string backupPath);
}
