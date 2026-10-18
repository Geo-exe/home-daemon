namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>An item as returned by Home Assistant <c>todo.get_items</c>.</summary>
public sealed record HomeAssistantTodoItem(
    string Uid,
    string Summary,
    string? Description,
    DateOnly? DueDate,
    HomeAssistantTodoStatus Status);
