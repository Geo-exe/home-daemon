namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// Outbound port used by the application layer to talk to Home Assistant. The concrete
/// HTTP adapter lives in the infrastructure project and is never referenced directly by
/// command or query handlers.
/// </summary>
public interface IHomeAssistantClient
{
    Task<IReadOnlyList<HomeAssistantTodoItem>> GetTodoItemsAsync(string todoListEntityId, CancellationToken cancellationToken);

    Task<HomeAssistantTodoItem> CreateTodoItemAsync(string todoListEntityId, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken);

    Task UpdateTodoItemAsync(string todoListEntityId, string itemUid, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken);

    Task DeleteTodoItemAsync(string todoListEntityId, string itemUid, CancellationToken cancellationToken);

    Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetCalendarEventsAsync(
        string calendarEntityId,
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken);
}
