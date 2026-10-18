using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Recurrence;

/// <summary>
/// Deterministic, anchor-preserving recurrence arithmetic.
/// </summary>
public sealed class RecurrenceCalculator : IRecurrenceCalculator
{
    public DateOnly GetNextOccurrence(DateOnly referenceDate, RecurrenceFrequency frequency, int interval)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(interval, 1);

        return frequency switch
        {
            RecurrenceFrequency.Daily => referenceDate.AddDays(interval),
            RecurrenceFrequency.Weekly => referenceDate.AddDays(7 * interval),
            RecurrenceFrequency.FixedIntervalDays => referenceDate.AddDays(interval),
            RecurrenceFrequency.Monthly => AddMonthsClamped(referenceDate, interval),
            RecurrenceFrequency.Yearly => AddYearsClamped(referenceDate, interval),
            _ => throw new ArgumentOutOfRangeException(nameof(frequency), frequency, "Unsupported recurrence frequency."),
        };
    }

    private static DateOnly AddMonthsClamped(DateOnly date, int months)
    {
        var targetMonth = new DateOnly(date.Year, date.Month, 1).AddMonths(months);
        var day = Math.Min(date.Day, DateTime.DaysInMonth(targetMonth.Year, targetMonth.Month));
        return new DateOnly(targetMonth.Year, targetMonth.Month, day);
    }

    private static DateOnly AddYearsClamped(DateOnly date, int years)
    {
        var targetYear = checked(date.Year + years);
        var day = Math.Min(date.Day, DateTime.DaysInMonth(targetYear, date.Month));
        return new DateOnly(targetYear, date.Month, day);
    }
}
