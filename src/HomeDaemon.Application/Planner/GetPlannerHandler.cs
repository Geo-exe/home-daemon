using HomeDaemon.Application.Abstractions;
using HomeDaemon.Application.Common;
using HomeDaemon.Application.Scheduling;
using HomeDaemon.Domain.Chores;
using Microsoft.EntityFrameworkCore;

namespace HomeDaemon.Application.Planner;

public sealed class GetPlannerHandler(IApplicationDbContext db, AvailabilityMatrixBuilder availabilityBuilder)
{
    public async Task<PlannerDto> HandleAsync(PlannerQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.To <= query.From)
        {
            throw new ApplicationValidationException("The planner end date must be after its start date.");
        }

        var household = await HouseholdLookup.GetRequiredAsync(db, cancellationToken).ConfigureAwait(false);
        var members = await db.HouseholdMembers
            .AsNoTracking()
            .Where(member => member.HouseholdId == household.Id && member.IsAssignable)
            .OrderBy(member => member.Name)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var calendarsByMember = await MemberCalendarReader.GetByMemberAsync(db, household.Id, cancellationToken).ConfigureAwait(false);
        var matrix = await availabilityBuilder.BuildAsync(household, members, calendarsByMember, query.From, query.To, cancellationToken).ConfigureAwait(false);

        var scheduled = await db.Chores
            .AsNoTracking()
            .Where(chore => chore.HouseholdId == household.Id
                && chore.ScheduleState == ChoreScheduleState.Scheduled
                && chore.NextDueDate >= query.From
                && chore.NextDueDate < query.To)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var unscheduled = await db.Chores
            .AsNoTracking()
            .Where(chore => chore.HouseholdId == household.Id && chore.ScheduleState == ChoreScheduleState.Unscheduled)
            .OrderBy(chore => chore.Title)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var days = BuildDays(query, members, matrix, scheduled);
        var unscheduledDtos = unscheduled
            .Select(chore => new PlannerUnscheduledDto(chore.Id, chore.Title, chore.UnscheduledReason ?? "Not placed."))
            .ToList();

        return new PlannerDto(query.From, query.To, matrix.CalendarDataAvailable, days, unscheduledDtos);
    }

    private static List<PlannerDayDto> BuildDays(
        PlannerQuery query,
        IReadOnlyList<Domain.Households.HouseholdMember> members,
        AvailabilityMatrix matrix,
        IReadOnlyList<ChoreDefinition> scheduled)
    {
        var scheduledMinutes = BuildScheduledMinutes(scheduled);
        var occurrencesByDay = scheduled
            .Where(chore => chore.NextDueDate is not null && chore.PlannedMemberId is not null)
            .GroupBy(chore => chore.NextDueDate!.Value)
            .ToDictionary(group => group.Key, group => group.ToList());

        var days = new List<PlannerDayDto>();
        for (var date = query.From; date < query.To; date = date.AddDays(1))
        {
            days.Add(BuildDay(date, members, matrix, scheduledMinutes, occurrencesByDay));
        }

        return days;
    }

    private static Dictionary<(DateOnly Date, Guid MemberId), int> BuildScheduledMinutes(IReadOnlyList<ChoreDefinition> scheduled)
    {
        var scheduledMinutes = new Dictionary<(DateOnly Date, Guid MemberId), int>();
        foreach (var chore in scheduled)
        {
            if (chore.NextDueDate is DateOnly date && chore.PlannedMemberId is Guid memberId)
            {
                var key = (date, memberId);
                scheduledMinutes[key] = (scheduledMinutes.TryGetValue(key, out var used) ? used : 0) + chore.EstimatedMinutes;
            }
        }

        return scheduledMinutes;
    }

    private static PlannerDayDto BuildDay(
        DateOnly date,
        IReadOnlyList<Domain.Households.HouseholdMember> members,
        AvailabilityMatrix matrix,
        Dictionary<(DateOnly Date, Guid MemberId), int> scheduledMinutes,
        Dictionary<DateOnly, List<ChoreDefinition>> occurrencesByDay)
    {
        var memberDays = new List<PlannerMemberDayDto>();
        foreach (var member in members)
        {
            if (!matrix.Availability.TryGetValue((date, member.Id), out var day))
            {
                continue;
            }

            var used = scheduledMinutes.TryGetValue((date, member.Id), out var minutes) ? minutes : 0;
            memberDays.Add(new PlannerMemberDayDto(
                member.Id,
                day.WindowMinutes,
                day.OccupiedMinutes,
                used,
                Math.Max(0, day.FreeMinutes - used),
                day.IsAllDayBlocked));
        }

        var occurrences = occurrencesByDay.TryGetValue(date, out var list)
            ? list.Select(chore => new PlannerOccurrenceDto(
                chore.Id,
                chore.Title,
                chore.RoomId,
                chore.EstimatedMinutes,
                chore.PlannedMemberId!.Value,
                chore.IsLocked,
                chore.NextOccurrenceDate ?? date)).ToList()
            : [];

        return new PlannerDayDto(date, memberDays, occurrences);
    }
}
