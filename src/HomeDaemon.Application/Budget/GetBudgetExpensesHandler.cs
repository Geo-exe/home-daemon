using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class GetBudgetExpensesHandler(IApplicationDbContext db)
{
    private const int DefaultPageSize = 50;
    private const int MaxPageSize = 200;

    public async Task<CursorPageDto<BudgetExpenseDto>> HandleAsync(GetBudgetExpensesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.From is DateOnly from && query.To is DateOnly to && to < from)
        {
            throw new ApplicationValidationException("The expense end date must not be before its start date.");
        }

        var pageSize = Math.Clamp(query.PageSize ?? DefaultPageSize, 1, MaxPageSize);

        var rows = await BuildQuery(query)
            .OrderByDescending(expense => expense.Date)
            .ThenByDescending(expense => expense.CreatedAtUtc)
            .ThenByDescending(expense => expense.Id)
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
        return new CursorPageDto<BudgetExpenseDto>(items, nextCursor, hasMore);
    }

    private IQueryable<BudgetExpense> BuildQuery(GetBudgetExpensesQuery query)
    {
        var expenses = db.BudgetExpenses
            .AsNoTracking()
            .Where(expense => expense.HouseholdId == query.HouseholdId);

        if (query.Year is int year)
        {
            var start = new DateOnly(year, 1, 1);
            var end = new DateOnly(year, 12, 31);
            expenses = expenses.Where(expense => expense.Date >= start && expense.Date <= end);
        }

        if (query.From is DateOnly fromDate)
        {
            expenses = expenses.Where(expense => expense.Date >= fromDate);
        }

        if (query.To is DateOnly toDate)
        {
            expenses = expenses.Where(expense => expense.Date <= toDate);
        }

        if (query.CategoryId is Guid categoryId)
        {
            expenses = expenses.Where(expense => expense.CategoryId == categoryId);
        }

        if (query.SubcategoryId is Guid subcategoryId)
        {
            expenses = expenses.Where(expense => expense.SubcategoryId == subcategoryId);
        }

        if (query.MemberId is Guid memberId)
        {
            expenses = expenses.Where(expense => expense.PaidByMemberId == memberId);
        }

        if (!string.IsNullOrWhiteSpace(query.Cursor))
        {
            var cursor = BudgetCursor.Parse(query.Cursor);
            expenses = expenses.Where(expense =>
                expense.Date < cursor.Date
                || (expense.Date == cursor.Date && expense.CreatedAtUtc < cursor.CreatedAtUtc)
                || (expense.Date == cursor.Date
                    && expense.CreatedAtUtc == cursor.CreatedAtUtc
                    && expense.Id.CompareTo(cursor.Id) < 0));
        }

        return expenses;
    }

    private async Task<IReadOnlyList<BudgetExpenseDto>> MapAsync(List<BudgetExpense> rows, CancellationToken cancellationToken)
    {
        if (rows.Count == 0)
        {
            return [];
        }

        var categoryIds = rows.Select(row => row.CategoryId).Distinct().ToList();
        var subcategoryIds = rows.Where(row => row.SubcategoryId is not null).Select(row => row.SubcategoryId!.Value).Distinct().ToList();
        var memberIds = rows.Select(row => row.PaidByMemberId).Distinct().ToList();

        var categoryNames = await db.BudgetCategories
            .AsNoTracking()
            .Where(category => categoryIds.Contains(category.Id))
            .Select(category => new { category.Id, category.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);

        var subcategoryNames = subcategoryIds.Count == 0
            ? new Dictionary<Guid, string>()
            : await db.BudgetSubcategories
                .AsNoTracking()
                .Where(subcategory => subcategoryIds.Contains(subcategory.Id))
                .Select(subcategory => new { subcategory.Id, subcategory.Name })
                .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
                .ConfigureAwait(false);

        var memberNames = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => memberIds.Contains(member.Id))
            .Select(member => new { member.Id, member.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken)
            .ConfigureAwait(false);

        return rows.Select(row => new BudgetExpenseDto(
            row.Id,
            row.Date,
            row.AmountMinor,
            row.CategoryId,
            categoryNames.GetValueOrDefault(row.CategoryId, string.Empty),
            row.SubcategoryId,
            row.SubcategoryId is Guid subcategoryId ? subcategoryNames.GetValueOrDefault(subcategoryId) : null,
            row.PaidByMemberId,
            memberNames.GetValueOrDefault(row.PaidByMemberId, string.Empty),
            row.Description)).ToList();
    }
}
