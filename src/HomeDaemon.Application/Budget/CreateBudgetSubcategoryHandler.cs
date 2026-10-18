using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Budget;

namespace HomeDaemon.Application.Budget;

public sealed class CreateBudgetSubcategoryHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetSubcategoryDto> HandleAsync(CreateBudgetSubcategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = BudgetGuards.RequireName(command.Name, "subcategory");
        var category = await BudgetGuards.RequireCategoryAsync(db, command.HouseholdId, command.CategoryId, cancellationToken).ConfigureAwait(false);
        await BudgetSubcategoryNames.RequireUniqueAsync(db, category.Id, name, null, cancellationToken).ConfigureAwait(false);

        var now = timeProvider.GetUtcNow();
        var subcategory = new BudgetSubcategory
        {
            HouseholdId = command.HouseholdId,
            CategoryId = category.Id,
            Name = name,
            SortOrder = command.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.BudgetSubcategories.Add(subcategory);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BudgetMappings.ToDto(subcategory);
    }
}
