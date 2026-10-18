namespace HomeDaemon.Domain.Chores;

/// <summary>
/// The recurring definition of a chore together with the state of its current cycle.
/// It keeps the three logical dates described in the technical analysis:
/// last completion, theoretical next occurrence and scheduled next due date.
/// </summary>
public sealed class ChoreDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public Guid? RoomId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    /// <summary>Estimated effort in whole minutes. Must be positive.</summary>
    public int EstimatedMinutes { get; set; }

    /// <summary>Optional difficulty/effort weight, above and beyond raw minutes.</summary>
    public double? EffortWeight { get; set; }

    /// <summary>Soft preference to place this chore near others of the same room.</summary>
    public bool PreferSameRoomGrouping { get; set; }

    public RecurrenceFrequency Frequency { get; set; }

    /// <summary>Multiplier applied to the base frequency unit, for example every 2 weeks.</summary>
    public int Interval { get; set; } = 1;

    public RecurrenceMode Mode { get; set; }

    /// <summary>Anchor used by anchored recurrences and as the creation reference.</summary>
    public DateOnly? AnchorDate { get; set; }

    public AssignmentPolicy AssignmentPolicy { get; set; }

    /// <summary>Target member for <see cref="AssignmentPolicy.SpecificMember"/>.</summary>
    public Guid? FixedAssigneeMemberId { get; set; }

    public DateOnly? LastCompletedDate { get; set; }

    public Guid? LastCompletedByMemberId { get; set; }

    /// <summary>Theoretical due date computed by the recurrence rule; never rewritten by the scheduler.</summary>
    public DateOnly? NextOccurrenceDate { get; set; }

    /// <summary>The date actually chosen by the scheduler and published to Home Assistant.</summary>
    public DateOnly? NextDueDate { get; set; }

    public Guid? PlannedMemberId { get; set; }

    /// <summary>Manual override protection applied until completion or explicit unlock.</summary>
    public bool IsLocked { get; set; }

    public ChoreScheduleState ScheduleState { get; set; }

    public string? UnscheduledReason { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
