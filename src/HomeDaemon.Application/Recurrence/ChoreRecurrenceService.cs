using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Recurrence;

public sealed class ChoreRecurrenceService(IRecurrenceCalculator calculator) : IChoreRecurrenceService
{
    public DateOnly GetNextAfterCompletion(ChoreDefinition chore, DateOnly completedDate)
    {
        ArgumentNullException.ThrowIfNull(chore);

        if (chore.Mode == RecurrenceMode.Dynamic)
        {
            return calculator.GetNextOccurrence(completedDate, chore.Frequency, chore.Interval);
        }

        var anchor = chore.AnchorDate ?? chore.NextOccurrenceDate ?? completedDate;
        var steps = 1;
        var candidate = Advance(anchor, chore.Frequency, chore.Interval * steps);
        while (candidate <= completedDate)
        {
            steps++;
            candidate = Advance(anchor, chore.Frequency, chore.Interval * steps);
        }

        return candidate;
    }

    public DateOnly GetInitialOccurrence(ChoreDefinition chore, DateOnly referenceDate)
    {
        ArgumentNullException.ThrowIfNull(chore);
        return chore.AnchorDate ?? referenceDate;
    }

    private DateOnly Advance(DateOnly from, RecurrenceFrequency frequency, int units) =>
        calculator.GetNextOccurrence(from, frequency, units);
}
