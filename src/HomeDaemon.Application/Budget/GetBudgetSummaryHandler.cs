using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Budget;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

/// <summary>
/// Builds the yearly summary. Everything is recomputed from the raw amounts, because the
/// proportion is annual and retroactive: any income change re-splits the whole year.
/// </summary>
public sealed class GetBudgetSummaryHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetSummaryDto> HandleAsync(GetBudgetSummaryQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        BudgetGuards.RequireYear(query.Year);

        var data = await LoadAsync(query, cancellationToken).ConfigureAwait(false);
        var balance = CalculateBalance(data);
        var missingByMember = BuildMissingMonths(data, query.Year);
        var byCategory = BuildCategoryTotals(data);

        return new BudgetSummaryDto(
            query.Year,
            data.Household.Currency,
            balance.TotalIncomeMinor,
            balance.TotalExpensesMinor,
            balance.ProportionAvailable,
            missingByMember.Values.All(missing => missing.Count == 0),
            BuildMemberSummaries(data, balance, missingByMember),
            byCategory);
    }

    private async Task<SummaryData> LoadAsync(GetBudgetSummaryQuery query, CancellationToken cancellationToken)
    {
        var household = await db.Households
            .AsNoTracking()
            .FirstOrDefaultAsync(item => item.Id == query.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Household '{query.HouseholdId}' was not found.");

        var members = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.HouseholdId == query.HouseholdId && member.IsBudgetParticipant)
            .OrderBy(member => member.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var yearStart = new DateOnly(query.Year, 1, 1);
        var yearEnd = new DateOnly(query.Year, 12, 31);

        var incomes = await db.BudgetIncomes
            .AsNoTracking()
            .Where(income => income.HouseholdId == query.HouseholdId && income.Year == query.Year)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var expenses = await db.BudgetExpenses
            .AsNoTracking()
            .Where(expense => expense.HouseholdId == query.HouseholdId && expense.Date >= yearStart && expense.Date <= yearEnd)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var transfers = await db.BudgetTransfers
            .AsNoTracking()
            .Where(transfer => transfer.HouseholdId == query.HouseholdId && transfer.SettlementYear == query.Year)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var categories = await db.BudgetCategories
            .AsNoTracking()
            .Where(category => category.HouseholdId == query.HouseholdId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new SummaryData(household, members, incomes, expenses, transfers, categories);
    }

    private static BudgetBalance CalculateBalance(SummaryData data)
    {
        var memberIds = data.Members.Select(member => member.Id).ToList();
        var incomeByMember = memberIds.ToDictionary(
            id => id,
            id => data.Incomes.Where(income => income.MemberId == id).Sum(income => income.AmountMinor));

        var expenseLines = data.Expenses
            .Select(expense => new BudgetExpenseLine(expense.PaidByMemberId, expense.AmountMinor))
            .ToList();

        var transferLines = data.Transfers
            .Select(transfer => new BudgetTransferLine(transfer.FromMemberId, transfer.ToMemberId, transfer.AmountMinor))
            .ToList();

        return BudgetBalanceCalculator.Calculate(memberIds, incomeByMember, expenseLines, transferLines);
    }

    private Dictionary<Guid, IReadOnlyList<int>> BuildMissingMonths(SummaryData data, int year)
    {
        var today = HouseholdClock.Today(data.Household, timeProvider);
        var throughMonth = year == today.Year ? today.Month : year < today.Year ? 12 : 0;

        var recordedMonths = data.Incomes
            .GroupBy(income => income.MemberId)
            .ToDictionary(group => group.Key, group => (IEnumerable<int>)group.Select(income => income.Month).ToList());

        var missingByMember = new Dictionary<Guid, IReadOnlyList<int>>();
        foreach (var member in data.Members)
        {
            var fromMonth = member.CreatedAtUtc.Year == year
                ? HouseholdClock.LocalDate(data.Household, member.CreatedAtUtc).Month
                : 1;
            var recorded = recordedMonths.TryGetValue(member.Id, out var months) ? months : [];
            missingByMember[member.Id] = BudgetIncomeStatus.FindMissingMonths(recorded, fromMonth, throughMonth);
        }

        return missingByMember;
    }

    private static List<BudgetMemberSummaryDto> BuildMemberSummaries(
        SummaryData data,
        BudgetBalance balance,
        Dictionary<Guid, IReadOnlyList<int>> missingByMember)
    {
        return balance.Members
            .Select(memberBalance =>
            {
                var member = data.Members.First(item => item.Id == memberBalance.MemberId);
                return new BudgetMemberSummaryDto(
                    memberBalance.MemberId,
                    member.Name,
                    memberBalance.AnnualIncomeMinor,
                    memberBalance.Share,
                    memberBalance.PaidMinor,
                    memberBalance.DueMinor,
                    memberBalance.TransferredOutMinor,
                    memberBalance.TransferredInMinor,
                    memberBalance.BalanceMinor,
                    missingByMember[memberBalance.MemberId]);
            })
            .ToList();
    }

    private static List<BudgetCategoryTotalDto> BuildCategoryTotals(SummaryData data)
    {
        var expenseByCategory = data.Expenses
            .GroupBy(expense => expense.CategoryId)
            .ToDictionary(group => group.Key, group => group.Sum(expense => expense.AmountMinor));

        return data.Categories
            .Select(category => new BudgetCategoryTotalDto(
                category.Id,
                category.Name,
                expenseByCategory.GetValueOrDefault(category.Id)))
            .Where(total => total.AmountMinor != 0)
            .OrderByDescending(total => total.AmountMinor)
            .ThenBy(total => total.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private sealed record SummaryData(
        Household Household,
        List<HouseholdMember> Members,
        List<BudgetIncome> Incomes,
        List<BudgetExpense> Expenses,
        List<BudgetTransfer> Transfers,
        List<BudgetCategory> Categories);
}
