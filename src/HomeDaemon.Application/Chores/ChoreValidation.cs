using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Chores;

internal static class ChoreValidation
{
    public static void Validate(string title, int estimatedMinutes, int interval, in double? effortWeight)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new ApplicationValidationException("The chore title is required.");
        }

        if (estimatedMinutes <= 0)
        {
            throw new ApplicationValidationException("The estimated duration must be a positive number of minutes.");
        }

        if (interval < 1)
        {
            throw new ApplicationValidationException("The recurrence interval must be at least one.");
        }

        if (effortWeight is < 0)
        {
            throw new ApplicationValidationException("The effort weight cannot be negative.");
        }
    }

    public static async Task EnsureRoomBelongsToHouseholdAsync(IApplicationDbContext db, Guid householdId, Guid? roomId, CancellationToken cancellationToken)
    {
        if (roomId is not Guid id)
        {
            return;
        }

        var exists = await db.Rooms
            .AsNoTracking()
            .AnyAsync(room => room.Id == id && room.HouseholdId == householdId, cancellationToken)
            .ConfigureAwait(false);

        if (!exists)
        {
            throw new ApplicationValidationException($"Room '{id}' does not belong to household '{householdId}'.");
        }
    }
}
