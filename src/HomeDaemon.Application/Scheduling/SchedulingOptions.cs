namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Explicit, named scoring weights. Keeping them as data avoids undocumented magic numbers
/// and lets tests pin down scheduling behaviour.
/// </summary>
public sealed record SchedulingOptions
{
    /// <summary>Weight applied to the absolute distance between the candidate day and the due date.</summary>
    public double DueDateDistanceWeight { get; init; } = 10.0;

    /// <summary>Extra weight applied only when a placement is after the due date.</summary>
    public double LatePenaltyWeight { get; init; } = 4.0;

    /// <summary>Weight that prefers days where the placement leaves more free time.</summary>
    public double RemainingCapacityWeight { get; init; } = 3.0;

    /// <summary>Weight that favours members carrying less load inside the horizon.</summary>
    public double LoadBalanceWeight { get; init; } = 1.0;

    /// <summary>Bonus subtracted when a priority room already has work on the candidate day.</summary>
    public double SameRoomBonus { get; init; } = 4.0;
}
