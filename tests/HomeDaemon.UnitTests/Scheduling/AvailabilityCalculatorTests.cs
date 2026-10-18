using HomeDaemon.Application.Scheduling;

namespace HomeDaemon.UnitTests.Scheduling;

public sealed class AvailabilityCalculatorTests
{
    private static readonly DateOnly Day = new(2026, 6, 15);
    private static readonly Guid MemberId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly TimeOnly WindowStart = new(8, 0);
    private static readonly TimeOnly WindowEnd = new(21, 0);

    [Fact]
    public void NoEvents_YieldsTheFullWindow()
    {
        var result = Calculate([]);

        result.WindowMinutes.Should().Be(780);
        result.OccupiedMinutes.Should().Be(0);
        result.FreeMinutes.Should().Be(780);
    }

    [Fact]
    public void EventInsideWindow_SubtractsTheWholeEvent()
    {
        var result = Calculate([Timed(new TimeOnly(10, 0), new TimeOnly(11, 30))]);

        result.OccupiedMinutes.Should().Be(90);
        result.FreeMinutes.Should().Be(690);
    }

    [Fact]
    public void EventPartiallyOutsideWindow_IsClippedToTheWindow()
    {
        var result = Calculate([Timed(new TimeOnly(7, 0), new TimeOnly(9, 0))]);

        result.OccupiedMinutes.Should().Be(60);
    }

    [Fact]
    public void OverlappingEvents_AreMergedAndCountedOnce()
    {
        var result = Calculate(
        [
            Timed(new TimeOnly(10, 0), new TimeOnly(11, 0)),
            Timed(new TimeOnly(10, 30), new TimeOnly(11, 30)),
        ]);

        result.OccupiedMinutes.Should().Be(90);
    }

    [Fact]
    public void AllDayEvent_BlocksTheWholeDay()
    {
        var allDay = new CalendarEvent(
            "Holiday",
            new DateTimeOffset(Day.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            new DateTimeOffset(Day.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
            IsAllDay: true);

        var result = Calculate([allDay]);

        result.IsAllDayBlocked.Should().BeTrue();
        result.FreeMinutes.Should().Be(0);
    }

    [Fact]
    public void MultiDayEvent_OnlySubtractsTheDailyOverlap()
    {
        var nextDay = Day.AddDays(1);
        var multiDay = new CalendarEvent(
            "Trip",
            new DateTimeOffset(Day.ToDateTime(new TimeOnly(20, 0)), TimeSpan.Zero),
            new DateTimeOffset(nextDay.ToDateTime(new TimeOnly(9, 0)), TimeSpan.Zero),
            IsAllDay: false);

        var firstDay = Calculate([multiDay]);
        var secondDay = AvailabilityCalculator.CalculateDay(nextDay, MemberId, WindowStart, WindowEnd, TimeZoneInfo.Utc, [multiDay], null);

        firstDay.OccupiedMinutes.Should().Be(60);
        secondDay.OccupiedMinutes.Should().Be(60);
    }

    [Fact]
    public void MaxMinutes_CapsTheFreeTime()
    {
        var result = Calculate([], maxMinutes: 90);

        result.FreeMinutes.Should().Be(90);
    }

    private static CalendarEvent Timed(TimeOnly start, TimeOnly end) =>
        new("Busy", new DateTimeOffset(Day.ToDateTime(start), TimeSpan.Zero), new DateTimeOffset(Day.ToDateTime(end), TimeSpan.Zero), IsAllDay: false);

    private static DailyAvailability Calculate(IReadOnlyList<CalendarEvent> events, int? maxMinutes = null) =>
        AvailabilityCalculator.CalculateDay(Day, MemberId, WindowStart, WindowEnd, TimeZoneInfo.Utc, events, maxMinutes);
}
