namespace HomeDaemon.Application.Planner;

public sealed record PlannerMemberDayDto(
    Guid MemberId,
    int WindowMinutes,
    int CalendarOccupiedMinutes,
    int ScheduledMinutes,
    int FreeMinutes,
    bool IsAllDayBlocked);
