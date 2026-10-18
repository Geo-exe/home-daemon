namespace HomeDaemon.Application.Households;

public sealed record CreateMemberCommand(
    Guid HouseholdId,
    string Name,
    string? Color,
    bool IsAssignable,
    string? TodoListEntityId,
    string? PersonEntityId,
    IReadOnlyList<string> CalendarEntityIds);
