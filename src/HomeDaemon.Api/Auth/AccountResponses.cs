using HomeDaemon.Api.Endpoints;
using HomeDaemon.DataAccess.Persistence;
using Microsoft.AspNetCore.Identity;

namespace HomeDaemon.Api.Auth;

/// <summary>Maps identity users and identity errors to the API contracts.</summary>
internal static class AccountResponses
{
    public static CurrentUserResponse ToCurrentUser(ApplicationUser user, IList<string> roles) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.DisplayName,
        user.HouseholdMemberId,
        user.MustChangePassword,
        [.. roles]);

    public static UserResponse ToUser(ApplicationUser user, IList<string> roles) => new(
        user.Id,
        user.Email ?? string.Empty,
        user.DisplayName,
        user.IsActive,
        user.MustChangePassword,
        user.HouseholdMemberId,
        [.. roles]);

    public static IResult ValidationProblem(IdentityResult result) =>
        Results.ValidationProblem(result.Errors
            .GroupBy(error => error.Code, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(error => error.Description).ToArray(),
                StringComparer.Ordinal));

    public static IResult InvalidCredentials() =>
        Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Invalid email or password.");
}
