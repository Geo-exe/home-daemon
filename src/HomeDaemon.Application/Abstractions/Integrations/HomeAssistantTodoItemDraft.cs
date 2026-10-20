namespace HomeDaemon.Application.Abstractions.Integrations;

/// <summary>
/// Values used to create or update a Home Assistant to-do item. The adapter sends the status
/// explicitly on updates; creation always starts as <c>needs_action</c> because the
/// <c>todo.add_item</c> action rejects the field.
/// </summary>
public sealed record HomeAssistantTodoItemDraft(
    string Summary,
    string? Description,
    DateOnly? DueDate,
    HomeAssistantTodoStatus Status);
