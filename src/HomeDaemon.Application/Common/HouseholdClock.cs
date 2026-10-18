using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Common;

/// <summary>
/// Converts UTC instants into the household's local operational date.
/// </summary>
public static class HouseholdClock
{
    public static DateOnly Today(Household household, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(timeProvider);
        return LocalDate(household, timeProvider.GetUtcNow());
    }

    public static DateOnly Today(string timeZoneId, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(timeProvider);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var local = TimeZoneInfo.ConvertTime(timeProvider.GetUtcNow(), timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static DateOnly LocalDate(Household household, DateTimeOffset instant)
    {
        ArgumentNullException.ThrowIfNull(household);
        var timeZone = TimeZoneInfo.FindSystemTimeZoneById(household.TimeZoneId);
        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }

    public static TimeZoneInfo TimeZone(Household household)
    {
        ArgumentNullException.ThrowIfNull(household);
        return TimeZoneInfo.FindSystemTimeZoneById(household.TimeZoneId);
    }
}
