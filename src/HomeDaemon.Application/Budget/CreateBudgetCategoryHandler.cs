using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Budget;

namespace HomeDaemon.Application.Budget;

public sealed class CreateBudgetCategoryHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetCategoryDto> HandleAsync(CreateBudgetCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = BudgetGuards.RequireName(command.Name, "category");
        await BudgetCategoryNames.RequireUniqueAsync(db, command.HouseholdId, name, null, cancellationToken).ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var category = new BudgetCategory
        {
            HouseholdId = command.HouseholdId,
            Name = name,
            SortOrder = command.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.BudgetCategories.Add(category);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BudgetMappings.ToDto(category, []);
    }
}
