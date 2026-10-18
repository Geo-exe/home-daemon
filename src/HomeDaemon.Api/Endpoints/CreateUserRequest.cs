namespace HomeDaemon.Api.Endpoints;

public sealed record CreateUserRequest(
    string Email,
    string? DisplayName,
    Guid? HouseholdMemberId,
    bool IsAdmin);
