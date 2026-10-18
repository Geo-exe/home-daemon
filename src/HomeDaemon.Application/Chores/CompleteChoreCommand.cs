using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

public sealed record CompleteChoreCommand(
    Guid ChoreId,
    DateOnly? CompletedDate,
    Guid? CompletedByMemberId,
    CompletionSource Source,
    string? IdempotencyKey);
