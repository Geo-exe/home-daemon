using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

/// <summary>
/// Deletes a room only when no chore still references it. Chores keep rooms as a soft
/// preference, so silently clearing the reference would hide an intent; the caller has to
/// reassign or clear the room first instead.
/// </summary>
public sealed class DeleteRoomHandler(IApplicationDbContext db)
{
    public async Task HandleAsync(DeleteRoomCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var room = await db.Rooms
            .FirstOrDefaultAsync(item => item.Id == command.RoomId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Room '{command.RoomId}' was not found.");

        // Chores are soft-deleted, so ignore the filter: a deleted chore still points at the
        // room and would otherwise leave an unreachable reference behind.
        var referenced = await db.Chores
            .IgnoreQueryFilters()
            .AsNoTracking()
            .AnyAsync(chore => chore.RoomId == room.Id, cancellationToken)
            .ConfigureAwait(false);

        if (referenced)
        {
            throw new EntityConflictException($"Room '{room.Id}' is still referenced by one or more chores.");
        }

        db.Rooms.Remove(room);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
