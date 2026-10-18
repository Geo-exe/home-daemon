using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Budget;

namespace HomeDaemon.Application.Budget;

public sealed class CreateBudgetExpenseHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetExpenseDto> HandleAsync(CreateBudgetExpenseCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        BudgetGuards.RequirePositive(command.AmountMinor, "expense");
        var category = await BudgetGuards.RequireCategoryAsync(db, command.HouseholdId, command.CategoryId, cancellationToken).ConfigureAwait(false);
        var subcategory = command.SubcategoryId is Guid subcategoryId
            ? await BudgetGuards.RequireSubcategoryAsync(db, command.HouseholdId, command.CategoryId, subcategoryId, cancellationToken).ConfigureAwait(false)
            : null;
        var member = await BudgetGuards.RequireParticipantAsync(db, command.HouseholdId, command.PaidByMemberId, cancellationToken).ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var expense = new BudgetExpense
        {
            HouseholdId = command.HouseholdId,
            Date = command.Date,
            AmountMinor = command.AmountMinor,
            CategoryId = category.Id,
            SubcategoryId = subcategory?.Id,
            PaidByMemberId = member.Id,
            Description = NullIfWhiteSpace(command.Description),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.BudgetExpenses.Add(expense);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BudgetMappings.ToDto(expense, category.Name, subcategory?.Name, member.Name);
    }

    internal static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
