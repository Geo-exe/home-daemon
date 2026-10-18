using System.Globalization;
using HomeDaemon.Application.Common;

namespace HomeDaemon.Application.Budget;

/// <summary>
/// Keyset cursor for the expense and transfer lists. It carries the full ordering tuple
/// (date, creation timestamp, id) so paging stays stable while new rows are inserted.
/// </summary>
internal sealed record BudgetCursor(DateOnly Date, DateTimeOffset CreatedAtUtc, Guid Id)
{
    public static BudgetCursor Parse(string value)
    {
        var parts = OpaqueCursor.Decode(value, 3);
        if (!DateOnly.TryParseExact(parts[0], "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            || !long.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var ticks)
            || !Guid.TryParse(parts[2], out var id))
        {
            throw new ApplicationValidationException("The cursor is not valid.");
        }

        return new BudgetCursor(date, new DateTimeOffset(ticks, TimeSpan.Zero), id);
    }

    public static string Format(DateOnly date, DateTimeOffset createdAtUtc, Guid id) =>
        OpaqueCursor.Encode(
            date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            createdAtUtc.UtcTicks.ToString(CultureInfo.InvariantCulture),
            id.ToString());
}
