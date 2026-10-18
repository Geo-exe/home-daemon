namespace HomeDaemon.Application.Households;

public sealed record UpdateMemberCommand(
    Guid MemberId,
    string Name,
    string? Color,
    bool IsAssignable,
    string? TodoListEntityId,
    string? PersonEntityId,
    IReadOnlyList<string> CalendarEntityIds);
