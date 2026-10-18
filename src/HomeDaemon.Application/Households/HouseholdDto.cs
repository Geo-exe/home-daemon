namespace HomeDaemon.Application.Households;

public sealed record HouseholdDto(
    Guid Id,
    string Name,
    string TimeZoneId,
    string Currency,
    int PlanningHorizonDays,
    TimeOnly DailyWindowStart,
    TimeOnly DailyWindowEnd,
    int? MaxChoreMinutesPerWeekday,
    int? MaxChoreMinutesPerWeekendDay);
