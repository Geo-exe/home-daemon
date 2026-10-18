namespace HomeDaemon.Application.Chores;

public sealed record RecordHomeAssistantCompletionCommand(
    string TodoListEntityId,
    string ItemUid,
    Guid? CompletedByMemberId,
    string? IdempotencyKey);
