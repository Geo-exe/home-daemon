using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Recurrence;

/// <summary>
/// Turns a chore definition plus a reference date into the next theoretical occurrence,
/// honouring the dynamic versus anchored mode.
/// </summary>
public interface IChoreRecurrenceService
{
    /// <summary>
    /// Computes the occurrence that follows an actual completion.
    /// Dynamic chores roll forward from the completion date; anchored chores keep the
    /// original anchor day and skip forward until the occurrence is strictly after it.
    /// </summary>
    DateOnly GetNextAfterCompletion(ChoreDefinition chore, DateOnly completedDate);

    /// <summary>
    /// Computes the first theoretical occurrence for a newly created chore.
    /// The explicit anchor wins; otherwise the provided reference date is used.
    /// </summary>
    DateOnly GetInitialOccurrence(ChoreDefinition chore, DateOnly referenceDate);
}
