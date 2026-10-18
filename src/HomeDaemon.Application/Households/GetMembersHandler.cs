using HomeDaemon.Application.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Households;

public sealed class GetMembersHandler(IApplicationDbContext db)
{
    public async Task<IReadOnlyList<HouseholdMemberDto>> HandleAsync(GetMembersQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var members = await db.HouseholdMembers
            .Where(member => member.HouseholdId == query.HouseholdId)
            .OrderBy(member => member.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var memberIds = members.ConvertAll(member => member.Id);
        var calendars = await db.HouseholdMemberCalendars
            .Where(calendar => memberIds.Contains(calendar.HouseholdMemberId))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var calendarsByMember = calendars
            .GroupBy(calendar => calendar.HouseholdMemberId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<string>)group.Select(item => item.CalendarEntityId).ToList());

        return members
            .Select(member => HouseholdMappings.ToDto(
                member,
                calendarsByMember.TryGetValue(member.Id, out var list) ? list : []))
            .ToList();
    }
}
