namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// Raised by the Home Assistant adapter. Transient failures (timeouts, 5xx, transport
/// errors) are retried; permanent ones (4xx, malformed responses) are surfaced as failed
/// outbox entries without losing the local plan.
/// </summary>
public sealed class HomeAssistantRequestException : Exception
{
    public HomeAssistantRequestException()
    {
    }

    public HomeAssistantRequestException(string message)
        : base(message)
    {
    }

    public HomeAssistantRequestException(string message, Exception innerException)
        : base(message, innerException) => IsTransient = false;

    public HomeAssistantRequestException(string message, bool isTransient)
        : base(message) => IsTransient = isTransient;

    public HomeAssistantRequestException(string message, Exception innerException, bool isTransient)
        : base(message, innerException) => IsTransient = isTransient;

    public bool IsTransient { get; }
}
