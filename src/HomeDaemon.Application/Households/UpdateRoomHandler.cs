using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class UpdateRoomHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<RoomDto> HandleAsync(UpdateRoomCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ApplicationValidationException("The room name is required.");
        }

        var room = await db.Rooms
            .FirstOrDefaultAsync(item => item.Id == command.RoomId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Room '{command.RoomId}' was not found.");

        room.Name = command.Name.Trim();
        room.SortOrder = command.SortOrder;
        room.UpdatedAtUtc = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(room);
    }
}
