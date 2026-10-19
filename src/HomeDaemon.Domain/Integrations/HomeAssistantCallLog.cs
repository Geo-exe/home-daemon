namespace HomeDaemon.Domain.Integrations;

/// <summary>
/// One outbound call to Home Assistant, recorded so an administrator can diagnose a failing
/// integration without access to the Home Assistant host. Only diagnostic metadata is stored:
/// no token and no request payload.
/// </summary>
public sealed class HomeAssistantCallLog
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public DateTimeOffset OccurredAtUtc { get; set; }

    /// <summary>Semantic action, for example <c>todo.add_item</c> or <c>calendar.get_events</c>.</summary>
    public string Operation { get; set; } = string.Empty;

    /// <summary>Home Assistant entity the call targeted, when the call is entity scoped.</summary>
    public string? EntityId { get; set; }

    /// <summary>HTTP method used by the adapter.</summary>
    public string Method { get; set; } = "GET";

    public HomeAssistantCallStatus Status { get; set; }

    /// <summary>HTTP status returned by Home Assistant, when a response was received.</summary>
    public int? StatusCode { get; set; }

    public int DurationMs { get; set; }

    /// <summary>Sanitized failure message; null for a successful call.</summary>
    public string? ErrorMessage { get; set; }

    /// <summary>Whether the adapter considered the failure transient and retryable.</summary>
    public bool? IsTransient { get; set; }
}
