namespace HomeDaemon.Api.Endpoints;

public sealed record TodoCompletionRequest(
    string TodoListEntityId,
    string ItemUid,
    Guid? CompletedByMemberId,
    string? IdempotencyKey);
