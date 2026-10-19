namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// Authenticated probe used by the Home Assistant custom integration. Reaching the health
/// endpoints only proves that the API is running; a successful handshake proves that the
/// presented machine API key is accepted and tells the integration which household it serves.
/// </summary>
public sealed record HomeAssistantHandshakeResponse(
    Guid HouseholdId,
    string HouseholdName,
    string Version);
