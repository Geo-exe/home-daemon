using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class UpdateBudgetCategoryHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetCategoryDto> HandleAsync(UpdateBudgetCategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = BudgetGuards.RequireName(command.Name, "category");

        var category = await db.BudgetCategories
            .FirstOrDefaultAsync(item => item.Id == command.CategoryId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Category '{command.CategoryId}' was not found.");

        await BudgetCategoryNames.RequireUniqueAsync(db, command.HouseholdId, name, category.Id, cancellationToken).ConfigureAwait(false);

        category.Name = name;
        category.SortOrder = command.SortOrder;
        category.IsArchived = command.IsArchived;
        category.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        var subcategories = await db.BudgetSubcategories
            .AsNoTracking()
            .Where(subcategory => subcategory.CategoryId == category.Id)
            .OrderBy(subcategory => subcategory.SortOrder)
            .ThenBy(subcategory => subcategory.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return BudgetMappings.ToDto(category, subcategories.Select(BudgetMappings.ToDto).ToList());
    }
}
