using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class CreateHouseholdHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    /// <summary>Currency applied when the client does not choose one.</summary>
    public const string DefaultCurrency = "EUR";

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

        // The currency is optional so clients that predate the first-run setup keep working;
        // fall back to the household default when it is not supplied.
        var currency = string.IsNullOrWhiteSpace(command.Currency) ? DefaultCurrency : command.Currency;
        if (!CurrencyCodes.IsValid(currency))
        {
            throw new ApplicationValidationException("The currency must be a three-letter ISO 4217 code.");
        }

        var now = timeProvider.GetUtcNow();
        var household = new Household
        {
            Name = command.Name.Trim(),
            TimeZoneId = command.TimeZoneId,
            Currency = CurrencyCodes.Normalize(currency),
            CreatedAtUtc = now,
            UpdatedAtUtc = now,
        };

        db.Households.Add(household);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(household);
    }
}
