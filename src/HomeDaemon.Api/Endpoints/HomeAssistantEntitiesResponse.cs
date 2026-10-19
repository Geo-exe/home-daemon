namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// The entities a settings picker can offer. <paramref name="Available"/> is false when the
/// integration is not configured, so the UI can fall back to manual entry without an error.
/// </summary>
public sealed record HomeAssistantEntitiesResponse(
    bool Available,
    IReadOnlyList<HomeAssistantEntityResponse> Entities);
