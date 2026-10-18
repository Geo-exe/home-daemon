namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// The signed-in account. <see cref="MustChangePassword"/> tells the client to route straight
/// to the password-change screen; the server enforces the same rule regardless.
/// </summary>
public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string? DisplayName,
    Guid? HouseholdMemberId,
    bool MustChangePassword,
    IReadOnlyList<string> Roles);
