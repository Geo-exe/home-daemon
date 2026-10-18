using System.Globalization;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Chores;

namespace HomeDaemon.Application.Chores;

/// <summary>
/// Keyset cursor for the completion history. It carries the full ordering tuple
/// (completed date, recording timestamp, id) so paging stays stable when new completions
/// are inserted while the client scrolls.
/// </summary>
internal sealed record ChoreHistoryCursor(DateOnly CompletedDate, DateTimeOffset RecordedAtUtc, Guid Id)
{
    public static ChoreHistoryCursor Parse(string value)
    {
        var parts = OpaqueCursor.Decode(value, 3);
        if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var completedDate)
            || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            || !Guid.TryParse(parts[2], out var id))
        {
            throw new ApplicationValidationException("The cursor is not valid.");
        }

        return new ChoreHistoryCursor(completedDate, new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    public static string Format(CompletionHistory history) =>
        OpaqueCursor.Encode(
            history.CompletedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            history.RecordedAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            history.Id.ToString());
}
