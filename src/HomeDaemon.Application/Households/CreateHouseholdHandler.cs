using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class CreateHouseholdHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<HouseholdDto> HandleAsync(CreateHouseholdCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (string.IsNullOrWhiteSpace(command.Name))
        {
            throw new ApplicationValidationException("The household name is required.");
        }

        if (!TimeZoneLookup.Exists(command.TimeZoneId))
        {
            throw new ApplicationValidationException($"Unknown timezone '{command.TimeZoneId}'.");
        }

        if (await db.Households.AnyAsync(cancellationToken).ConfigureAwait(false))
        {
            throw new ApplicationValidationException("A household already exists.");
        }

        var now = timeProvider.GetUtcNow();
        var household = new Household
        {
            Name = command.Name.Trim(),
            TimeZoneId = command.TimeZoneId,
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Households.Add(household);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(household);
    }
}
