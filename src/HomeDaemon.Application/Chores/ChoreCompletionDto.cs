using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

public sealed record ChoreCompletionDto(
    Guid Id,
    DateOnly CompletedDate,
    DateTimeOffset RecordedAtUtc,
    CompletionSource Source,
    Guid? CompletedByMemberId,
    Guid? CompletedByUserId);
