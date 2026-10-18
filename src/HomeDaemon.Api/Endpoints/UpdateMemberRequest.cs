namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateMemberRequest(
    string Name,
    string? Color,
    bool IsAssignable,
    string? TodoListEntityId,
    string? PersonEntityId,
    IReadOnlyList<string> CalendarEntityIds);
