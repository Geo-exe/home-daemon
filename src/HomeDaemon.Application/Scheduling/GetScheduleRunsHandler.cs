using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Returns the recalculation runs as cursor-paginated pages, newest first. It is read-only
/// diagnostics: it never starts a run.
/// </summary>
public sealed class GetScheduleRunsHandler(IApplicationDbContext db)
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 100;

    public async Task<CursorPageDto<ScheduleRunDto>> HandleAsync(GetScheduleRunsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var runs = db.ScheduleRuns
            .AsNoTracking()
            .Where(run => run.HouseholdId == query.HouseholdId);

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var cursor = ScheduleRunsCursor.Parse(query.Cursor);
            runs = runs.Where(run =>
                run.StartedAtUtc < cursor.StartedAtUtc
                || (run.StartedAtUtc == cursor.StartedAtUtc && run.Id.CompareTo(cursor.Id) < 0));
        }

        var rows = await runs
            .OrderByDescending(run => run.StartedAtUtc)
            .ThenByDescending(run => run.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var items = rows.ConvertAll(ScheduleRunMappings.ToDto);
        var nextCursor = hasMore ? ScheduleRunsCursor.Format(rows[^1]) : null;
        return new CursorPageDto<ScheduleRunDto>(items, nextCursor, hasMore);
    }
}
