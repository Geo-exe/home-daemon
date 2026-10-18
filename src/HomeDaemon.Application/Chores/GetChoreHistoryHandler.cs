using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// Returns the completion history as cursor-paginated pages, newest first. Because the chore
/// may have been soft-deleted, titles are resolved while ignoring the global query filter so
/// the history stays readable.
/// </summary>
public sealed class GetChoreHistoryHandler(IApplicationDbContext db)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public async Task<CursorPageDto<ChoreHistoryItemDto>> HandleAsync(GetChoreHistoryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From is DateOnly from && query.To is DateOnly to && to < from)
        {
            throw new ApplicationValidationException("The history end date must not be before its start date.");
        }

        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var rows = await BuildQuery(query)
            .OrderByDescending(item => item.CompletedDate)
            .ThenByDescending(item => item.RecordedAtUtc)
            .ThenByDescending(item => item.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var items = await MapAsync(rows, cancellationToken).ConfigureAwait(false);
        var nextCursor = hasMore ? ChoreHistoryCursor.Format(rows[^1]) : null;
        return new CursorPageDto<ChoreHistoryItemDto>(items, nextCursor, hasMore);
    }

    private IQueryable<CompletionHistory> BuildQuery(GetChoreHistoryQuery query)
    {
        var history = db.CompletionHistory
            .AsNoTracking()
            .Where(item => item.HouseholdId == query.HouseholdId);

        if (query.ChoreId is Guid choreId)
        {
            history = history.Where(item => item.ChoreDefinitionId == choreId);
        }

        if (query.MemberId is Guid memberId)
        {
            history = history.Where(item => item.CompletedByMemberId == memberId);
        }

        if (query.From is DateOnly fromDate)
        {
            history = history.Where(item => item.CompletedDate >= fromDate);
        }

        if (query.To is DateOnly toDate)
        {
            history = history.Where(item => item.CompletedDate <= toDate);
        }

        if (query.Source is CompletionSource source)
        {
            history = history.Where(item => item.Source == source);
        }

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var cursor = ChoreHistoryCursor.Parse(query.Cursor);
            history = history.Where(item =>
                item.CompletedDate < cursor.CompletedDate
                || (item.CompletedDate == cursor.CompletedDate && item.RecordedAtUtc < cursor.RecordedAtUtc)
                || (item.CompletedDate == cursor.CompletedDate
                    && item.RecordedAtUtc == cursor.RecordedAtUtc
                    && item.Id.CompareTo(cursor.Id) < 0));
        }

        return history;
    }

    private async Task<IReadOnlyList<ChoreHistoryItemDto>> MapAsync(
        List<CompletionHistory> rows,
        CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var choreIds = rows.Select(row => row.ChoreDefinitionId).Distinct().ToList();
        var titles = await db.Chores
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(chore => choreIds.Contains(chore.Id))
            .Select(chore => new { chore.Id, chore.Title })
            .ToDictionaryAsync(item => item.Id, item => item.Title, cancellationToken)
            .ConfigureAwait(false);

        var memberIds = rows
            .Where(row => row.CompletedByMemberId is not null)
            .Select(row => row.CompletedByMemberId!.Value)
            .Distinct()
            .ToList();

        var memberNames = memberIds.Count == 0
            ? []
            : await db.HouseholdMembers
                .AsNoTracking()
                .Where(member => memberIds.Contains(member.Id))
                .Select(member => new { member.Id, member.Name })
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
                .ConfigureAwait(false);

        return rows.Select(row => new ChoreHistoryItemDto(
            row.Id,
            row.ChoreDefinitionId,
            titles.TryGetValue(row.ChoreDefinitionId, out var title) ? title : string.Empty,
            row.CompletedDate,
            row.RecordedAtUtc,
            row.Source,
            row.CompletedByMemberId,
            row.CompletedByMemberId is Guid memberId && memberNames.TryGetValue(memberId, out var name) ? name : null)).ToList();
    }
}
