namespace HomeDaemon.Application.Households;

public sealed record HouseholdMemberDto(
    Guid Id,
    string Name,
    string? Color,
    bool IsAssignable,
    string? TodoListEntityId,
    string? PersonEntityId,
    IReadOnlyList<string> CalendarEntityIds);
