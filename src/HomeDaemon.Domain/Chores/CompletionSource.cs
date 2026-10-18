namespace HomeDaemon.Domain.Chores;

/// <summary>How a completion reached the backend.</summary>
public enum CompletionSource
{
    HomeAssistant = 0,
    Api = 1,
    Manual = 2,
}
