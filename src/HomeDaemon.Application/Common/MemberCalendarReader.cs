using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Common;

internal static class MemberCalendarReader
{
    public static async Task<IReadOnlyDictionary<Guid, IReadOnlyList<string>>> GetByMemberAsync(
        IApplicationDbContext db,
        Guid householdId,
        CancellationToken cancellationToken)
    {
        var rows = await db.HouseholdMemberCalendars
            .AsNoTracking()
            .Join(
                db.HouseholdMembers.AsNoTracking().Where(member => member.HouseholdId == householdId),
                calendar => calendar.HouseholdMemberId,
                member => member.Id,
                (calendar, member) => new { calendar.HouseholdMemberId, calendar.CalendarEntityId })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return rows
            .GroupBy(row => row.HouseholdMemberId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyList<string>)group.Select(row => row.CalendarEntityId).ToList());
    }
}
