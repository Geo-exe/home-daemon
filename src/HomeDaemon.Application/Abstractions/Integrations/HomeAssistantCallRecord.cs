namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// Diagnostic record of one outbound Home Assistant call. It deliberately carries no request
/// headers or payload, so the machine credential can never reach the diagnostic log.
/// </summary>
public sealed record HomeAssistantCallRecord(
    string Operation,
    string? EntityId,
    string Method,
    bool Succeeded,
    int? StatusCode,
    long DurationMs,
    string? ErrorMessage,
    bool? IsTransient);
