using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// Query for the paginated completion history. All filters are optional; the household scope
/// is always applied by the caller. <see cref="To"/> is inclusive.
/// </summary>
public sealed record GetChoreHistoryQuery(
    Guid HouseholdId,
    Guid? ChoreId,
    Guid? MemberId,
    DateOnly? From,
    DateOnly? To,
    CompletionSource? Source,
    string? Cursor,
    int? PageSize);
