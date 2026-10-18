namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// Values used to create or update a Home Assistant to-do item. The status is always sent
/// explicitly so reusing an item that was just completed resets it to <c>needs_action</c>.
/// </summary>
public sealed record HomeAssistantTodoItemDraft(
    string Summary,
    string? Description,
    DateOnly? DueDate,
    HomeAssistantTodoStatus Status);
