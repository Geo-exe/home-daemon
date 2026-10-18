using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Households;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class UpdateHouseholdHandler(IApplicationDbContext db, TimeProvider timeProvider)
{
    public async Task<HouseholdDto> HandleAsync(UpdateHouseholdCommand command, CancellationToken cancellationToken)
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

        if (command.PlanningHorizonDays is < 1 or > 365)
        {
            throw new ApplicationValidationException("The planning horizon must be between 1 and 365 days.");
        }

        if (command.DailyWindowEnd <= command.DailyWindowStart)
        {
            throw new ApplicationValidationException("The daily window end must be after its start.");
        }

        var household = await db.Households
            .FirstOrDefaultAsync(item => item.Id == command.HouseholdId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new EntityNotFoundException($"Household '{command.HouseholdId}' was not found.");

        household.Name = command.Name.Trim();
        household.TimeZoneId = command.TimeZoneId;
        household.PlanningHorizonDays = command.PlanningHorizonDays;
        household.DailyWindowStart = command.DailyWindowStart;
        household.DailyWindowEnd = command.DailyWindowEnd;
        household.MaxChoreMinutesPerWeekday = command.MaxChoreMinutesPerWeekday;
        household.MaxChoreMinutesPerWeekendDay = command.MaxChoreMinutesPerWeekendDay;
        household.UpdatedAtUtc = timeProvider.GetUtcNow();

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return HouseholdMappings.ToDto(household);
    }
}
