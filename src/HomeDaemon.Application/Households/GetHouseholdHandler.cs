using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;

namespace HomeDaemon.Application.Households;

public sealed class GetHouseholdHandler(IApplicationDbContext db)
{
    public async Task<HouseholdDto> HandleAsync(GetHouseholdQuery query, CancellationToken cancellationToken)
    {
        _ = query;
        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(household);
    }
}
