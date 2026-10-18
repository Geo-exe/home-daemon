using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class GetBudgetYearsHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<IReadOnlyList<int>> HandleAsync(GetBudgetYearsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var incomeYears = await db.BudgetIncomes
            .AsNoTracking()
            .Where(income => income.HouseholdId == query.HouseholdId)
            .Select(income => income.Year)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var expenseDates = await db.BudgetExpenses
            .AsNoTracking()
            .Where(expense => expense.HouseholdId == query.HouseholdId)
            .Select(expense => expense.Date)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transferYears = await db.BudgetTransfers
            .AsNoTracking()
            .Where(transfer => transfer.HouseholdId == query.HouseholdId)
            .Select(transfer => transfer.SettlementYear)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return incomeYears
            .Concat(expenseDates.Select(date => date.Year))
            .Concat(transferYears)
            .Append(timeProvider.GetUtcNow().Year)
            .Distinct()
            .OrderByDescending(year => year)
            .ToList();
    }
}
