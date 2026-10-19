using System.Globalization;
using HomeDaemon.Application.Common;

namespace HomeDaemon.Application.Integrations.HomeAssistant;

/// <summary>Keyset cursor for the diagnostic log page, ordered newest first.</summary>
internal sealed record HomeAssistantLogCursor(DateTimeOffset OccurredAtUtc, Guid Id)
{
    public static HomeAssistantLogCursor Parse(string value)
    {
        var parts = OpaqueCursor.Decode(value, 2);
        if (!long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            || !Guid.TryParse(parts[1], out var id))
        {
            throw new ApplicationValidationException("The cursor is not valid.");
        }

        return new HomeAssistantLogCursor(new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    public static string Format(DateTimeOffset occurredAtUtc, Guid id) =>
        OpaqueCursor.Encode(
            occurredAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            id.ToString());
}
