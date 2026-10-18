using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class GetBudgetIncomesHandler(IApplicationDbContext db)
{
    public async Task<BudgetIncomesDto> HandleAsync(GetBudgetIncomesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        BudgetGuards.RequireYear(query.Year);

        var currency = await db.Households
            .AsNoTracking()
            .Where(household => household.Id == query.HouseholdId)
            .Select(household => household.Currency)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? "EUR";

        var members = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.HouseholdId == query.HouseholdId && member.IsBudgetParticipant)
            .OrderBy(member => member.Name)
            .Select(member => new { member.Id, member.Name })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var incomes = await db.BudgetIncomes
            .AsNoTracking()
            .Where(income => income.HouseholdId == query.HouseholdId && income.Year == query.Year)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byMember = incomes
            .GroupBy(income => income.MemberId)
            .ToDictionary(group => group.Key, group => group.ToDictionary(income => income.Month));

        var memberDtos = members
            .Select(member =>
            {
                byMember.TryGetValue(member.Id, out var months);
                var cells = new List<BudgetIncomeMonthDto>(12);
                for (var month = 1; month <= 12; month++)
                {
                    cells.Add(months is not null && months.TryGetValue(month, out var income)
                        ? new BudgetIncomeMonthDto(month, income.AmountMinor, income.Id)
                        : new BudgetIncomeMonthDto(month, null, null));
                }

                return new BudgetMemberIncomesDto(member.Id, member.Name, cells);
            })
            .ToList();

        return new BudgetIncomesDto(query.Year, currency, memberDtos);
    }
}
