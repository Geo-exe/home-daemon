using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Common;

/// <summary>
/// Resolves the single configured household. The first version runs one household per
/// instance; multi-household support is a deliberate future concern.
/// </summary>
public static class HouseholdLookup
{
    public static async Task<Household?> FindAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        return await db.Households
            .OrderBy(household => household.CreatedAtUtc)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    public static async Task<Household> GetRequiredAsync(IApplicationDbContext db, CancellationToken cancellationToken)
    {
        var household = await FindAsync(db, cancellationToken).ConfigureAwait(false);
        return household ?? throw new EntityNotFoundException("No household has been configured yet.");
    }
}
