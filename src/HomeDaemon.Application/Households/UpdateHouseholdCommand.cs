namespace HomeDaemon.Application.Households;

public sealed record UpdateHouseholdCommand(
    Guid HouseholdId,
    string Name,
    string TimeZoneId,
    int PlanningHorizonDays,
    TimeOnly DailyWindowStart,
    TimeOnly DailyWindowEnd,
    int? MaxChoreMinutesPerWeekday,
    int? MaxChoreMinutesPerWeekendDay);
