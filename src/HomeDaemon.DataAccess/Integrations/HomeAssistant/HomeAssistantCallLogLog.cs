using Microsoft.Extensions.Logging;

namespace HomeDaemon.DataAccess.Integrations.HomeAssistant;

/// <summary>Source-generated log messages for the Home Assistant call diagnostics.</summary>
internal static partial class HomeAssistantCallLogLog
{
    [LoggerMessage(EventId = 6001, Level = LogLevel.Warning, Message = "Could not persist a Home Assistant call log entry.")]
    public static partial void PersistFailed(ILogger logger, Exception exception);
}
