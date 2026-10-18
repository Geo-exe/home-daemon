using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Budget;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class UpsertBudgetIncomeHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetIncomeMonthDto> HandleAsync(UpsertBudgetIncomeCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        BudgetGuards.RequireYear(command.Year);
        BudgetGuards.RequireMonth(command.Month);
        BudgetGuards.RequireNonNegative(command.AmountMinor, "income");
        _ = await BudgetGuards.RequireParticipantAsync(db, command.HouseholdId, command.MemberId, cancellationToken).ConfigureAwait(false);

        var income = await db.BudgetIncomes
            .FirstOrDefaultAsync(
                item => item.HouseholdId == command.HouseholdId
                    && item.MemberId == command.MemberId
                    && item.Year == command.Year
                    && item.Month == command.Month,
                cancellationToken)
            .ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        if (income is null)
        {
            income = new BudgetIncome
            {
                HouseholdId = command.HouseholdId,
                MemberId = command.MemberId,
                Year = command.Year,
                Month = command.Month,
                AmountMinor = command.AmountMinor,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };

            db.BudgetIncomes.Add(income);
        }
        else
        {
            income.AmountMinor = command.AmountMinor;
            income.UpdatedAtUtc = now;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BudgetMappings.ToDto(income);
    }
}
