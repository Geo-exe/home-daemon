namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// Sink for outbound Home Assistant call diagnostics. The adapter never depends on how the
/// record is stored, and recording a log must never break the call it describes.
/// </summary>
public interface IHomeAssistantCallRecorder
{
    void Record(HomeAssistantCallRecord record);
}
