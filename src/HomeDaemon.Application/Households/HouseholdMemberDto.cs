namespace HomeDaemon.Application.Households;

public sealed record HouseholdMemberDto(
    Guid Id,
    string Name,
    string? Color,
    bool IsAssignable,
    bool IsBudgetParticipant,
    string? TodoListEntityId,
    string? PersonEntityId,
    IReadOnlyList<string> CalendarEntityIds);
