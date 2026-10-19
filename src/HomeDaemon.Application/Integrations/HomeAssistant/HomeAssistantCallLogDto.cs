using HomeDaemon.Domain.Integrations;

namespace HomeDaemon.Application.Integrations.HomeAssistant;

public sealed record HomeAssistantCallLogDto(
    Guid Id,
    DateTimeOffset OccurredAtUtc,
    string Operation,
    string? EntityId,
    string Method,
    HomeAssistantCallStatus Status,
    int? StatusCode,
    int DurationMs,
    string? ErrorMessage,
    bool? IsTransient);
