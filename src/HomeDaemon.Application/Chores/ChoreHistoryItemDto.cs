using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// A single completion in the paged history. The chore title is resolved even when the chore
/// has since been soft-deleted, so history stays readable.
/// </summary>
public sealed record ChoreHistoryItemDto(
    Guid Id,
    Guid ChoreDefinitionId,
    string ChoreTitle,
    DateOnly CompletedDate,
    DateTimeOffset RecordedAtUtc,
    CompletionSource Source,
    Guid? CompletedByMemberId,
    string? CompletedByMemberName);
