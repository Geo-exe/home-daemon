using HomeDaemon.Domain.Households;

namespace HomeDaemon.Application.Households;

internal static class HouseholdMappings
{
    public static HouseholdDto ToDto(Household household) => new(
        household.Id,
        household.Name,
        household.TimeZoneId,
        household.PlanningHorizonDays,
        household.DailyWindowStart,
        household.DailyWindowEnd,
        household.MaxChoreMinutesPerWeekday,
        household.MaxChoreMinutesPerWeekendDay);

    public static HouseholdMemberDto ToDto(HouseholdMember member, IReadOnlyList<string> calendarEntityIds) => new(
        member.Id,
        member.Name,
        member.Color,
        member.IsAssignable,
        member.TodoListEntityId,
        member.PersonEntityId,
        calendarEntityIds);

    public static RoomDto ToDto(Room room) => new(room.Id, room.Name, room.SortOrder);
}
