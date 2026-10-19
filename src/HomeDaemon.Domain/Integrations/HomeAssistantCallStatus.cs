namespace HomeDaemon.Domain.Integrations;

/// <summary>Outcome of one call HomeDaemon made to Home Assistant.</summary>
public enum HomeAssistantCallStatus
{
    Succeeded = 0,
    Failed = 1,
}
