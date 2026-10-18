namespace HomeDaemon.Api.Endpoints;

public sealed record UpdateUserRequest(
    string Email,
    string? DisplayName,
    Guid? HouseholdMemberId,
    bool IsAdmin);
