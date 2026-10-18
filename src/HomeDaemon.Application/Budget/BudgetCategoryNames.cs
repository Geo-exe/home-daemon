using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

/// <summary>Keeps category names unique within a household, ignoring case.</summary>
internal static class BudgetCategoryNames
{
    public static async Task RequireUniqueAsync(
        IApplicationDbContext db,
        Guid householdId,
        string name,
        Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var names = await db.BudgetCategories
            .AsNoTracking()
            .Where(category => category.HouseholdId == householdId && (excludeId == null || category.Id != excludeId))
            .Select(category => category.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (names.Any(existing => string.Equals(existing, name, StringComparison.OrdinalIgnoreCase)))
        {
            throw new EntityConflictException($"A category named '{name}' already exists.");
        }
    }
}
