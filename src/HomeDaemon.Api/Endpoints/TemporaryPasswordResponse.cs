namespace HomeDaemon.Api.Endpoints;

/// <summary>
/// The one and only time the temporary password is returned. The account must change it at the
/// next sign-in.
/// </summary>
public sealed record TemporaryPasswordResponse(UserResponse User, string TemporaryPassword);
