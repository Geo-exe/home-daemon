namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateHouseholdRequest(
    string Name,
    string TimeZoneId,
    int PlanningHorizonDays,
    TimeOnly DailyWindowStart,
    TimeOnly DailyWindowEnd,
    int? MaxChoreMinutesPerWeekday,
    int? MaxChoreMinutesPerWeekendDay);
