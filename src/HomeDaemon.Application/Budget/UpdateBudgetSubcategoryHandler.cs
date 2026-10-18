using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Budget;

public sealed class UpdateBudgetSubcategoryHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<BudgetSubcategoryDto> HandleAsync(UpdateBudgetSubcategoryCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var name = BudgetGuards.RequireName(command.Name, "subcategory");

        var subcategory = await db.BudgetSubcategories
            .FirstOrDefaultAsync(item => item.Id == command.SubcategoryId && item.HouseholdId == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Subcategory '{command.SubcategoryId}' was not found.");

        await BudgetSubcategoryNames.RequireUniqueAsync(db, subcategory.CategoryId, name, subcategory.Id, cancellationToken).ConfigureAwait(false);

        subcategory.Name = name;
        subcategory.SortOrder = command.SortOrder;
        subcategory.IsArchived = command.IsArchived;
        subcategory.UpdatedAtUtc = timeProvider.GetUtcNow();
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return BudgetMappings.ToDto(subcategory);
    }
}
