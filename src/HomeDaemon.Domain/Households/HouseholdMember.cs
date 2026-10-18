namespace HomeDaemon.Domain.Households;

/// <summary>
/// A person that can receive chores. A domain person is intentionally distinct from an
/// application login account; a household member may exist without ever signing in.
/// </summary>
public sealed class HouseholdMember
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdId { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Presentation accent colour, for example <c>#4F46E5</c>.</summary>
    public string? Color { get; set; }

    /// <summary>Whether the scheduler may assign chores to this member.</summary>
    public bool IsAssignable { get; set; } = true;

    /// <summary>
    /// Whether this person takes part in the shared budget. Only participants can be used in
    /// the budget module; it is independent of chore assignment.
    /// </summary>
    public bool IsBudgetParticipant { get; set; }

    /// <summary>Home Assistant to-do list entity that receives this member's planned chores.</summary>
    public string? TodoListEntityId { get; set; }

    /// <summary>Optional Home Assistant <c>person.*</c> entity used for identity matching.</summary>
    public string? PersonEntityId { get; set; }

    /// <summary>Optional member-specific cap for Monday-Friday chore minutes.</summary>
    public int? MaxChoreMinutesPerWeekday { get; set; }

    /// <summary>Optional member-specific cap for Saturday-Sunday chore minutes.</summary>
    public int? MaxChoreMinutesPerWeekendDay { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
