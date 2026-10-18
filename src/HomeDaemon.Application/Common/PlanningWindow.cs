using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Common;

/// <summary>
/// Rolling planning window, expressed as civil dates with an exclusive end so the interval
/// length is unambiguous and configurable through the household.
/// </summary>
public sealed record PlanningWindow(DateOnly FromInclusive, DateOnly ToExclusive)
{
    public DateOnly ToInclusive => ToExclusive.AddDays(-1);

    public bool Contains(DateOnly date) => date >= FromInclusive && date < ToExclusive;

    public static PlanningWindow For(Household household, DateOnly today)
    {
        ArgumentNullException.ThrowIfNull(household);
        return new PlanningWindow(today, today.AddDays(Math.Max(1, household.PlanningHorizonDays)));
    }
}
