namespace HomeDaemon.Domain.Chores;

/// <summary>Observable scheduling state of the current chore cycle.</summary>
public enum ChoreScheduleState
{
    /// <summary>Not yet evaluated, for example outside the planning horizon.</summary>
    Unplanned = 0,

    /// <summary>A concrete date and member were chosen.</summary>
    Scheduled = 1,

    /// <summary>Evaluated but no valid placement was found; see <c>UnscheduledReason</c>.</summary>
    Unscheduled = 2,

    /// <summary>Paused definition, excluded from scheduling.</summary>
    Paused = 3,
}
