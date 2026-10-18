using HomeDaemon.Application.Recurrence;
using HomeDaemon.Domain.Chores;

namespace HomeDaemon.UnitTests.Recurrence;

public sealed class ChoreRecurrenceServiceTests
{
    private readonly ChoreRecurrenceService _service = new(new RecurrenceCalculator());

    [Fact]
    public void Dynamic_RollsForwardFromTheCompletionDate()
    {
        var chore = new ChoreDefinition { Frequency = RecurrenceFrequency.Daily, Interval = 2, Mode = RecurrenceMode.Dynamic };

        var result = _service.GetNextAfterCompletion(chore, new DateOnly(2026, 5, 1));

        result.Should().Be(new DateOnly(2026, 5, 3));
    }

    [Fact]
    public void Anchored_KeepsTheAnchorDay()
    {
        var chore = new ChoreDefinition
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            Mode = RecurrenceMode.Anchored,
            AnchorDate = new DateOnly(2026, 1, 15),
        };

        var result = _service.GetNextAfterCompletion(chore, new DateOnly(2026, 1, 20));

        result.Should().Be(new DateOnly(2026, 2, 15));
    }

    [Fact]
    public void Anchored_DoesNotDriftForEndOfMonthAnchors()
    {
        var chore = new ChoreDefinition
        {
            Frequency = RecurrenceFrequency.Monthly,
            Interval = 1,
            Mode = RecurrenceMode.Anchored,
            AnchorDate = new DateOnly(2026, 1, 31),
        };

        var result = _service.GetNextAfterCompletion(chore, new DateOnly(2026, 3, 1));

        result.Should().Be(new DateOnly(2026, 3, 31));
    }

    [Fact]
    public void InitialOccurrence_UsesTheAnchorWhenPresent()
    {
        var chore = new ChoreDefinition { AnchorDate = new DateOnly(2026, 4, 1) };

        var result = _service.GetInitialOccurrence(chore, new DateOnly(2026, 3, 15));

        result.Should().Be(new DateOnly(2026, 4, 1));
    }

    [Fact]
    public void InitialOccurrence_FallsBackToTheReferenceDate()
    {
        var chore = new ChoreDefinition();

        var result = _service.GetInitialOccurrence(chore, new DateOnly(2026, 3, 15));

        result.Should().Be(new DateOnly(2026, 3, 15));
    }
}
