namespace HomeDaemon.Api.Auth;

/// <summary>Role names used by the authorization policies.</summary>
public static class AccountRoles
{
    public const string Admin = "Admin";
    public const string User = "User";

    /// <summary>Authorization policy that only administrators satisfy.</summary>
    public const string AdminPolicy = "AdminOnly";
}
