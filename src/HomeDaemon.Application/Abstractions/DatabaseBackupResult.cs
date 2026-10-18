namespace HomeDaemon.Application.Abstractions;

public sealed record DatabaseBackupResult(string FilePath, DateTimeOffset CreatedAtUtc, long SizeBytes);
