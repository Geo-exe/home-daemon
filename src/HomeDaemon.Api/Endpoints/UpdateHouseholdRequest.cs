namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateHouseholdRequest(
    string Name,
    string TimeZoneId,
    string Currency,
    int PlanningHorizonDays,
    TimeOnly DailyWindowStart,
    TimeOnly DailyWindowEnd,
    int? MaxChoreMinutesPerWeekday,
    int? MaxChoreMinutesPerWeekendDay);
