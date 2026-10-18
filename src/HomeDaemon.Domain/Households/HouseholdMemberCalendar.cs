namespace HomeDaemon.Domain.Households;

/// <summary>
/// Associates a Home Assistant calendar entity with a member. A member may have several
/// calendars; a shared calendar may be linked to more than one member, and overlapping
/// events are merged during availability calculation.
/// </summary>
public sealed class HouseholdMemberCalendar
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid HouseholdMemberId { get; set; }

    /// <summary>Home Assistant <c>calendar.*</c> entity id.</summary>
    public string CalendarEntityId { get; set; } = string.Empty;

    public DateTimeOffset CreatedAtUtc { get; set; }
}
