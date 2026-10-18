using Microsoft.Extensions.Logging;

namespace HomeDaemon.Application.Scheduling;

/// <summary>Source-generated, allocation-free log messages for scheduling.</summary>
internal static partial class SchedulingLog
{
    [LoggerMessage(EventId = 4001, Level = LogLevel.Error, Message = "Scheduling recalculation {RunId} failed.")]
    public static partial void RecalculationFailed(ILogger logger, Guid runId, Exception exception);
}
