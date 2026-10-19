namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// One state object exposed by Home Assistant. Only the identity, domain and display name are
/// kept: the integration never mirrors arbitrary state attributes into the application.
/// </summary>
public sealed record HomeAssistantEntity(string EntityId, string Domain, string? FriendlyName);
