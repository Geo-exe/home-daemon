using HomeDaemon.Domain.Integrations;

namespace HomeDaemon.Application.Integrations.HomeAssistant;

public sealed record GetHomeAssistantCallLogsQuery(
    HomeAssistantCallStatus? Status,
    string? Operation,
    string? EntityId,
    string? Cursor,
    int? PageSize);
