namespace HomeDaemon.DataAccess.Backup;

public sealed class BackupOptions
{
    /// <summary>
    /// Destination directory for SQLite backups. It should live outside the database volume
    /// or container, for example a mounted host path.
    /// </summary>
    public string? Directory { get; set; }

    /// <summary>Maximum number of backup files to keep; older files are removed.</summary>
    public int RetentionCount { get; set; } = 14;
}
