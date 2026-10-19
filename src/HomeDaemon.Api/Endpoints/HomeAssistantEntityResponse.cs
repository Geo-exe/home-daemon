namespace HomeDaemon.Api.Endpoints;

public sealed record HomeAssistantEntityResponse(string EntityId, string Domain, string? FriendlyName);
