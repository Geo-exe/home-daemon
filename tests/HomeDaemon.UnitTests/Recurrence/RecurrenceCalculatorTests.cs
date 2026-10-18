using HomeDaemon.Application.Recurrence;
using HomeDaemon.Domain.Chores;

namespace HomeDaemon.UnitTests.Recurrence;

public sealed class RecurrenceCalculatorTests
{
    private readonly RecurrenceCalculator _calculator = new();

    [Fact]
    public void Daily_AddsTheIntervalInDays()
    {
        var result = _calculator.GetNextOccurrence(new DateOnly(2026, 1, 10), RecurrenceFrequency.Daily, 3);

        result.Should().Be(new DateOnly(2026, 1, 13));
    }

    [Fact]
    public void Weekly_AddsWholeWeeks()
    {
        var result = _calculator.GetNextOccurrence(new DateOnly(2026, 1, 10), RecurrenceFrequency.Weekly, 2);

        result.Should().Be(new DateOnly(2026, 1, 24));
    }

    [Fact]
    public void FixedIntervalDays_AddsTheInterval()
    {
        var result = _calculator.GetNextOccurrence(new DateOnly(2026, 1, 10), RecurrenceFrequency.FixedIntervalDays, 45);

        result.Should().Be(new DateOnly(2026, 2, 24));
    }

    [Theory]
    [InlineData(2026, 1, 31, 2026, 2, 28)]
    [InlineData(2028, 1, 31, 2028, 2, 29)]
    public void Monthly_ClampsToTheLastDayOfShorterMonths(int year, int month, int day, int expectedYear, int expectedMonth, int expectedDay)
    {
        var result = _calculator.GetNextOccurrence(new DateOnly(year, month, day), RecurrenceFrequency.Monthly, 1);

        result.Should().Be(new DateOnly(expectedYear, expectedMonth, expectedDay));
    }

    [Fact]
    public void Yearly_ClampsLeapDayInNonLeapYears()
    {
        var result = _calculator.GetNextOccurrence(new DateOnly(2028, 2, 29), RecurrenceFrequency.Yearly, 1);

        result.Should().Be(new DateOnly(2029, 2, 28));
    }

    [Fact]
    public void IntervalLessThanOne_IsRejected()
    {
        var act = () => _calculator.GetNextOccurrence(new DateOnly(2026, 1, 1), RecurrenceFrequency.Daily, 0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
