using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

public sealed class GetChoresHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<ChoreListItemDto>> HandleAsync(GetChoresQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var choresQuery = db.Chores
            .AsNoTracking()
            .Where(chore => chore.HouseholdId == query.HouseholdId);

        if (query.RoomId is Guid roomId)
        {
            choresQuery = choresQuery.Where(chore => chore.RoomId == roomId);
        }

        if (query.IsActive is bool isActive)
        {
            choresQuery = choresQuery.Where(chore => chore.IsActive == isActive);
        }

        var chores = await choresQuery
            .OrderBy(chore => chore.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return chores.ConvertAll(ChoreMappings.ToListItem);
    }
}
