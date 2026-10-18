using HomeDaemon.Application.Abstractions.Integrations;
using HomeDaemon.Application.Common;
using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Scheduling;

/// <summary>
/// Fetches member calendars from Home Assistant and turns them into a per-day availability
/// matrix. A failing calendar degrades to "no events" for that member while recording a
/// diagnostic, so the scheduler can still run.
/// </summary>
public sealed class AvailabilityMatrixBuilder(IHomeAssistantClient homeAssistant)
{
    public async Task<AvailabilityMatrix> BuildAsync(
        Household household,
        IReadOnlyList<HouseholdMember> members,
        IReadOnlyDictionary<Guid, IReadOnlyList<string>> calendarsByMember,
        DateOnly fromInclusive,
        DateOnly toExclusive,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(household);
        ArgumentNullException.ThrowIfNull(members);
        ArgumentNullException.ThrowIfNull(calendarsByMember);

        var timeZone = HouseholdClock.TimeZone(household);
        var diagnostics = new List<string>();
        var calendarDataAvailable = true;
        var eventsByMember = new Dictionary<Guid, List<CalendarEvent>>();

        foreach (var member in members)
        {
            var events = new List<CalendarEvent>();
            if (calendarsByMember.TryGetValue(member.Id, out var calendars))
            {
                foreach (var calendarEntityId in calendars)
                {
                    try
                    {
                        var fetched = await homeAssistant
                            .GetCalendarEventsAsync(calendarEntityId, fromInclusive, toExclusive, cancellationToken)
                            .ConfigureAwait(false);

                        foreach (var item in fetched)
                        {
                            events.Add(new CalendarEvent(item.Summary, item.StartsAt, item.EndsAt, item.IsAllDay));
                        }
                    }
                    catch (Exception ex) when (!cancellationToken.IsCancellationRequested
                        && ex is HomeAssistantRequestException or HttpRequestException or TaskCanceledException)
                    {
                        calendarDataAvailable = false;
                        diagnostics.Add($"Calendar '{calendarEntityId}' for member '{member.Name}' could not be read: {ex.Message}");
                    }
                }
            }

            eventsByMember[member.Id] = events;
        }

        var availability = new Dictionary<(DateOnly Date, Guid MemberId), DailyAvailability>();
        for (var date = fromInclusive; date < toExclusive; date = date.AddDays(1))
        {
            var isWeekend = date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
            foreach (var member in members)
            {
                var memberEvents = eventsByMember.TryGetValue(member.Id, out var list) ? list : [];
                var cap = ResolveDailyCap(household, member, isWeekend);
                availability[(date, member.Id)] = AvailabilityCalculator.CalculateDay(
                    date,
                    member.Id,
                    household.DailyWindowStart,
                    household.DailyWindowEnd,
                    timeZone,
                    memberEvents,
                    cap);
            }
        }

        return new AvailabilityMatrix(availability, diagnostics, calendarDataAvailable);
    }

    private static int? ResolveDailyCap(Household household, HouseholdMember member, bool isWeekend)
    {
        var memberCap = isWeekend ? member.MaxChoreMinutesPerWeekendDay : member.MaxChoreMinutesPerWeekday;
        var householdCap = isWeekend ? household.MaxChoreMinutesPerWeekendDay : household.MaxChoreMinutesPerWeekday;

        return (memberCap, householdCap) switch
        {
            (int memberValue, int householdValue) => Math.Min(memberValue, householdValue),
            (int memberValue, null) => memberValue,
            (null, int householdValue) => householdValue,
            _ => null,
        };
    }
}
