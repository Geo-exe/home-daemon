using HomeDaemon.Application.Abstractions.Integrations;

namespace HomeDaemon.IntegrationTests.Support;

/// <summary>Home Assistant stub that reports empty calendars and no to-do items.</summary>
public sealed class NoOpHomeAssistantClient : IHomeAssistantClient
{
    public Task<IReadOnlyList<HomeAssistantTodoItem>> GetTodoItemsAsync(string todoListEntityId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HomeAssistantTodoItem>>([]);

    public Task<HomeAssistantTodoItem> CreateTodoItemAsync(string todoListEntityId, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken) =>
        Task.FromResult(new HomeAssistantTodoItem("uid", draft.Summary, draft.Description, draft.DueDate, draft.Status));

    public Task UpdateTodoItemAsync(string todoListEntityId, string itemUid, HomeAssistantTodoItemDraft draft, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task DeleteTodoItemAsync(string todoListEntityId, string itemUid, CancellationToken cancellationToken) =>
        Task.CompletedTask;

    public Task<IReadOnlyList<HomeAssistantCalendarEvent>> GetCalendarEventsAsync(string calendarEntityId, DateOnly fromInclusive, DateOnly toExclusive, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<HomeAssistantCalendarEvent>>([]);
}
