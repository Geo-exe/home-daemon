using System.Globalization;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Scheduling;

namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Keyset cursor for the recalculation run list, ordered by start time then id.
/// </summary>
internal sealed record ScheduleRunsCursor(DateTimeOffset StartedAtUtc, Guid Id)
{
    public static ScheduleRunsCursor Parse(string value)
    {
        var parts = OpaqueCursor.Decode(value, 2);
        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            || !Guid.TryParse(parts[1], out var id))
        {
            throw new ApplicationValidationException("The cursor is not valid.");
        }

        return new ScheduleRunsCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    public static string Format(ScheduleRun run) =>
        OpaqueCursor.Encode(
            run.StartedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            run.Id.ToString());
}
