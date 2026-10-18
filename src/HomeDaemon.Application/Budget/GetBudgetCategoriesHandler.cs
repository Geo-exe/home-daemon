using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class GetBudgetCategoriesHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<BudgetCategoryDto>> HandleAsync(GetBudgetCategoriesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var categories = await db.BudgetCategories
            .AsNoTracking()
            .Where(category => category.HouseholdId == query.HouseholdId)
            .OrderBy(category => category.SortOrder)
            .ThenBy(category => category.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var subcategories = await db.BudgetSubcategories
            .AsNoTracking()
            .Where(subcategory => subcategory.HouseholdId == query.HouseholdId)
            .OrderBy(subcategory => subcategory.SortOrder)
            .ThenBy(subcategory => subcategory.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var byCategory = subcategories
            .GroupBy(subcategory => subcategory.CategoryId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<BudgetSubcategoryDto>)group.Select(BudgetMappings.ToDto).ToList());

        return categories
            .Select(category => BudgetMappings.ToDto(
                category,
                byCategory.TryGetValue(category.Id, out var list) ? list : []))
            .ToList();
    }
}
