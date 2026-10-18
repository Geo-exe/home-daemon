using HomeDaemon.Application.Abstractions;
using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Households;

internal static class HouseholdTextHelpers
{
    public static void AddCalendars(IApplicationDbContext db, Guid memberId, IEnumerable<string> calendarEntityIds, DateTimeOffset now)
    {
        foreach (var entityId in Clean(calendarEntityIds))
        {
            db.HouseholdMemberCalendars.Add(new HouseholdMemberCalendar
            {
                HouseholdMemberId = memberId,
                CalendarEntityId = entityId,
                CreatedAtUtc = now,
            });
        }
    }

    public static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    public static IReadOnlyList<string> Clean(IEnumerable<string>? values) =>
        values is null
            ? []
            : values
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
}
