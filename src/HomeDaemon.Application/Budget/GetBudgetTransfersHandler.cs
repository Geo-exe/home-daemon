using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class GetBudgetTransfersHandler(IApplicationDbContext db)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public async Task<CursorPageDto<BudgetTransferDto>> HandleAsync(GetBudgetTransfersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var transfers = db.BudgetTransfers
            .AsNoTracking()
            .Where(transfer => transfer.HouseholdId == query.HouseholdId);

        if (query.SettlementYear is int year)
        {
            transfers = transfers.Where(transfer => transfer.SettlementYear == year);
        }

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var cursor = BudgetCursor.Parse(query.Cursor);
            transfers = transfers.Where(transfer =>
                transfer.Date < cursor.Date
                || (transfer.Date == cursor.Date && transfer.CreatedAtUtc < cursor.CreatedAtUtc)
                || (transfer.Date == cursor.Date
                    && transfer.CreatedAtUtc == cursor.CreatedAtUtc
                    && transfer.Id.CompareTo(cursor.Id) < 0));
        }

        var rows = await transfers
            .OrderByDescending(transfer => transfer.Date)
            .ThenByDescending(transfer => transfer.CreatedAtUtc)
            .ThenByDescending(transfer => transfer.Id)
            .Take(pageSize + 1)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var hasMore = rows.Count > pageSize;
        if (hasMore)
        {
            rows.RemoveAt(rows.Count - 1);
        }

        var items = await MapAsync(rows, cancellationToken).ConfigureAwait(false);
        var nextCursor = hasMore ? BudgetCursor.Format(rows[^1].Date, rows[^1].CreatedAtUtc, rows[^1].Id) : null;
        return new CursorPageDto<BudgetTransferDto>(items, nextCursor, hasMore);
    }

    private async Task<IReadOnlyList<BudgetTransferDto>> MapAsync(List<BudgetTransfer> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var memberIds = rows
            .SelectMany(row => new[] { row.FromMemberId, row.ToMemberId })
            .Distinct()
            .ToList();

        var memberNames = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .Select(member => new { member.Id, member.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(row => new BudgetTransferDto(
            row.Id,
            row.Date,
            row.SettlementYear,
            row.AmountMinor,
            row.FromMemberId,
            memberNames.GetValueOrDefault(row.FromMemberId, string.Empty),
            row.ToMemberId,
            memberNames.GetValueOrDefault(row.ToMemberId, string.Empty),
            row.Description)).ToList();
    }
}
