using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class GetRoomsHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<RoomDto>> HandleAsync(GetRoomsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var rooms = await db.Rooms
            .Where(room => room.HouseholdId == query.HouseholdId)
            .OrderBy(room => room.SortOrder)
            .ThenBy(room => room.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rooms.ConvertAll(HouseholdMappings.ToDto);
    }
}
