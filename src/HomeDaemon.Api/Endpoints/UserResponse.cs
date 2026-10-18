namespace HomeDaemon.Api.Endpoints;

public sealed record UserResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    bool IsActive,
    bool MustChangePassword,
    Guid? HouseholdMemberId,
    IReadOnlyList<string> Roles);
