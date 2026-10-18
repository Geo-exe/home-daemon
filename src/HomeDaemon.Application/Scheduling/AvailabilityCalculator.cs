namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Pure computation of daily availability from calendar events.
/// Only the overlap of an event with the local scheduling window is subtracted, so a
/// multi-day or very long event never removes its full duration from every day it spans.
/// </summary>
public static class AvailabilityCalculator
{
    public static int ComputeWindowMinutes(TimeOnly start, TimeOnly end)
    {
        var minutes = (int)(end - start).TotalMinutes;
        if (minutes <= 0)
        {
            throw new ArgumentException("The daily window end must be after its start.", nameof(end));
        }

        return minutes;
    }

    public static DailyAvailability CalculateDay(
        DateOnly date,
        Guid memberId,
        TimeOnly windowStart,
        TimeOnly windowEnd,
        TimeZoneInfo timeZone,
        IReadOnlyList<CalendarEvent> events,
        int? maxMinutes)
    {
        ArgumentNullException.ThrowIfNull(timeZone);
        ArgumentNullException.ThrowIfNull(events);

        var windowMinutes = ComputeWindowMinutes(windowStart, windowEnd);
        var windowStartInstant = ToInstant(date, windowStart, timeZone);
        var windowEndInstant = ToInstant(date, windowEnd, timeZone);

        if (events.Any(e => e.IsAllDay && e.StartsAt < windowEndInstant && e.EndsAt > windowStartInstant))
        {
            return new DailyAvailability(date, memberId, windowMinutes, windowMinutes, 0, true);
        }

        var occupied = MergeOverlappingMinutes(events, windowStartInstant, windowEndInstant);
        var free = Math.Max(0, windowMinutes - occupied);
        if (maxMinutes is int cap && cap >= 0)
        {
            free = Math.Min(free, cap);
        }

        return new DailyAvailability(date, memberId, windowMinutes, occupied, free, false);
    }

    private static DateTimeOffset ToInstant(DateOnly date, TimeOnly time, TimeZoneInfo timeZone)
    {
        var local = date.ToDateTime(time, DateTimeKind.Unspecified);
        var offset = timeZone.GetUtcOffset(local);
        return new DateTimeOffset(local, offset);
    }

    private static int MergeOverlappingMinutes(
        IReadOnlyList<CalendarEvent> events,
        DateTimeOffset windowStart,
        DateTimeOffset windowEnd)
    {
        var intervals = new List<(DateTimeOffset Start, DateTimeOffset End)>();
        foreach (var calendarEvent in events)
        {
            if (calendarEvent.IsAllDay)
            {
                continue;
            }

            var start = calendarEvent.StartsAt > windowStart ? calendarEvent.StartsAt : windowStart;
            var end = calendarEvent.EndsAt < windowEnd ? calendarEvent.EndsAt : windowEnd;
            if (end > start)
            {
                intervals.Add((start, end));
            }
        }

        if (intervals.Count == 0)
        {
            return 0;
        }

        intervals.Sort(static (left, right) => left.Start.CompareTo(right.Start));
        var totalMinutes = 0.0;
        var currentStart = intervals[0].Start;
        var currentEnd = intervals[0].End;
        for (var i = 1; i < intervals.Count; i++)
        {
            var (start, end) = intervals[i];
            if (start <= currentEnd)
            {
                if (end > currentEnd)
                {
                    currentEnd = end;
                }
            }
            else
            {
                totalMinutes += (currentEnd - currentStart).TotalMinutes;
                currentStart = start;
                currentEnd = end;
            }
        }

        totalMinutes += (currentEnd - currentStart).TotalMinutes;
        return (int)Math.Round(totalMinutes, MidpointRounding.AwayFromZero);
    }
}
