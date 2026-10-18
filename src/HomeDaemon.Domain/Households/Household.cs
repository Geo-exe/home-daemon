namespace HomeDaemon.Domain.Households;

/// <summary>
/// The root of the shared domestic core. It carries settings used by every module,
/// most importantly the local timezone that turns calendar instants into operational dates.
/// </summary>
public sealed class Household
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public string Name { get; set; } = string.Empty;

    public string TimeZoneId { get; set; } = "UTC";

    /// <summary>Rolling planning window, expressed in civil days from "today".</summary>
    public int PlanningHorizonDays { get; set; } = 30;

    /// <summary>Start of the local daily window that chores can be scheduled into, inclusive.</summary>
    public TimeOnly DailyWindowStart { get; set; } = new(8, 0);

    /// <summary>End of the local daily window that chores can be scheduled into, exclusive.</summary>
    public TimeOnly DailyWindowEnd { get; set; } = new(21, 0);

    /// <summary>Optional realistic cap on scheduled chore minutes for Monday-Friday.</summary>
    public int? MaxChoreMinutesPerWeekday { get; set; }

    /// <summary>Optional realistic cap on scheduled chore minutes for Saturday-Sunday.</summary>
    public int? MaxChoreMinutesPerWeekendDay { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
