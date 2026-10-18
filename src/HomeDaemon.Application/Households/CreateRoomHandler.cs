using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class CreateRoomHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<RoomDto> HandleAsync(CreateRoomCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ApplicationValidationException("The room name is required.");
        }

        _ = await db.Households
            .FirstOrDefaultAsync(item => item.Id == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Household '{command.HouseholdId}' was not found.");

        var now = timeProvider.GetUtcNow();
        var room = new Room
        {
            HouseholdId = command.HouseholdId,
            Name = command.Name.Trim(),
            SortOrder = command.SortOrder,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Rooms.Add(room);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(room);
    }
}
