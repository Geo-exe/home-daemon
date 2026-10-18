namespace HomeDaemon.Api.Endpoints;

public sealed record CreateMemberRequest(
    string Name,
    string? Color,
    bool IsAssignable,
    string? TodoListEntityId,
    string? PersonEntityId,
    IReadOnlyList<string> CalendarEntityIds);
