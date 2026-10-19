using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Integrations;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Integrations.HomeAssistant;

/// <summary>
/// Reads the outbound Home Assistant call diagnostics, newest first, with optional filters.
/// This is an administrator-only surface; the endpoint enforces the role.
/// </summary>
public sealed class GetHomeAssistantCallLogsHandler(IApplicationDbContext db)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public async Task<CursorPageDto<HomeAssistantCallLogDto>> HandleAsync(
        GetHomeAssistantCallLogsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);
        var logs = ApplyFilters(db.HomeAssistantCallLogs.AsNoTracking(), query);

        var rows = await logs
            .OrderByDescending(log => log.OccurredAtUtc)
            .ThenByDescending(log => log.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var items = rows.Select(ToDto).ToList();

        var nextCursor = hasMore
            ? HomeAssistantLogCursor.Format(rows[^1].OccurredAtUtc, rows[^1].Id)
            : null;

        return new CursorPageDto<HomeAssistantCallLogDto>(items, nextCursor, hasMore);
    }

    private static IQueryable<HomeAssistantCallLog> ApplyFilters(
        IQueryable<HomeAssistantCallLog> logs,
        GetHomeAssistantCallLogsQuery query)
    {
        if (query.Status is HomeAssistantCallStatus status)
        {
            logs = logs.Where(log => log.Status == status);
        }

        var operation = query.Operation?.Trim();
        if (!string.IsNullOrEmpty(operation))
        {
            logs = logs.Where(log => log.Operation.Contains(operation));
        }

        var entityId = query.EntityId?.Trim();
        if (!string.IsNullOrEmpty(entityId))
        {
            logs = logs.Where(log => log.EntityId != null && log.EntityId.Contains(entityId));
        }

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var cursor = HomeAssistantLogCursor.Parse(query.Cursor);
            logs = logs.Where(log =>
                log.OccurredAtUtc < cursor.OccurredAtUtc
                || (log.OccurredAtUtc == cursor.OccurredAtUtc && log.Id.CompareTo(cursor.Id) < 0));
        }

        return logs;
    }

    private static HomeAssistantCallLogDto ToDto(HomeAssistantCallLog log) =>
        new(
            log.Id,
            log.OccurredAtUtc,
            log.Operation,
            log.EntityId,
            log.Method,
            log.Status,
            log.StatusCode,
            log.DurationMs,
            log.ErrorMessage,
            log.IsTransient);
}
