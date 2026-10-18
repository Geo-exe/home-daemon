namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateMemberRequest(
    string Name,
    string? Color,
    bool IsAssignable,
    bool IsBudgetParticipant,
    string? TodoListEntityId,
    string? PersonEntityId,
    IReadOnlyList<string> CalendarEntityIds);
