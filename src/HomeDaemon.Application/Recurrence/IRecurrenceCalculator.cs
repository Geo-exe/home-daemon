namespace HomeDaemon.Application.Recurrence;

/// <summary>
/// Pure calendar arithmetic for chore recurrences. Implementations must be deterministic
/// and free of database, clock or network access so they can be covered by unit tests.
/// </summary>
public interface IRecurrenceCalculator
{
    /// <summary>
    /// Returns the occurrence that follows <paramref name="referenceDate"/> for the given
    /// frequency and interval. Monthly and yearly recurrences clamp to the last valid day
    /// of the target month instead of rolling into the next month.
    /// </summary>
    DateOnly GetNextOccurrence(DateOnly referenceDate, HomeDaemon.Domain.Chores.RecurrenceFrequency frequency, int interval);
}
