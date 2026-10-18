namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>Home Assistant to-do item status, mirroring the native <c>todo</c> entity.</summary>
public enum HomeAssistantTodoStatus
{
    NeedsAction = 0,
    Completed = 1,
}
