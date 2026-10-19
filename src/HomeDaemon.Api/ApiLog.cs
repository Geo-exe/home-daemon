using Microsoft.Extensions.Logging;

namespace HomeDaemon.Api;

/// <summary>Source-generated, allocation-free log messages for the API host and workers.</summary>
public static partial class ApiLog
{
    [LoggerMessage(EventId = 9001, Level = LogLevel.Error, Message = "Unhandled exception while processing {Path}.")]
    public static partial void UnhandledRequest(ILogger logger, string path, Exception exception);

    [LoggerMessage(EventId = 9002, Level = LogLevel.Error, Message = "Schedule run worker failed for run {RunId}.")]
    public static partial void ScheduleRunFailed(ILogger logger, Guid runId, Exception exception);

    [LoggerMessage(EventId = 9003, Level = LogLevel.Error, Message = "Nightly recalculation trigger failed.")]
    public static partial void NightlyRecalculationFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9004, Level = LogLevel.Warning, Message = "Could not determine the next nightly run time; retrying shortly.")]
    public static partial void NightlyDelayFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9005, Level = LogLevel.Information, Message = "Database backup worker is disabled by configuration.")]
    public static partial void BackupDisabled(ILogger logger);

    [LoggerMessage(EventId = 9006, Level = LogLevel.Error, Message = "Database backup failed.")]
    public static partial void BackupFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9007, Level = LogLevel.Warning, Message = "Could not determine the next backup time; retrying shortly.")]
    public static partial void BackupDelayFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9008, Level = LogLevel.Error, Message = "To-do sync pass failed.")]
    public static partial void TodoSyncPassFailed(ILogger logger, Exception exception);

    [LoggerMessage(EventId = 9009, Level = LogLevel.Warning, Message = "Could not list Home Assistant entities.")]
    public static partial void HomeAssistantEntitiesFailed(ILogger logger, Exception exception);
}
