using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// Records a completion. <see cref="CompletedByUserId"/> and <see cref="CompletedByMemberId"/>
/// are resolved server-side from the authenticated account; they are never taken from the
/// request body.
/// </summary>
public sealed record CompleteChoreCommand(
    Guid ChoreId,
    DateOnly? CompletedDate,
    Guid? CompletedByMemberId,
    CompletionSource Source,
    string? IdempotencyKey,
    Guid? CompletedByUserId = null);
