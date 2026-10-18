namespace HomeDaemon.Application.Planner;

public sealed record PlannerOccurrenceDto(
    Guid ChoreId,
    string Title,
    Guid? RoomId,
    int EstimatedMinutes,
    Guid MemberId,
    bool IsLocked,
    DateOnly DueDate);
