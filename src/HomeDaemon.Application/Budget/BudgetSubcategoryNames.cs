using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

/// <summary>Keeps subcategory names unique within their category, ignoring case.</summary>
internal static class BudgetSubcategoryNames
{
    public static async Task RequireUniqueAsync(
        IApplicationDbContext db,
        Guid categoryId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var names = await db.BudgetSubcategories
            .AsNoTracking()
            .Where(subcategory => subcategory.CategoryId == categoryId && (excludeId == null || subcategory.Id != excludeId))
            .Select(subcategory => subcategory.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (names.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new EntityConflictException($"A subcategory named '{name}' already exists in this category.");
        }
    }
}
