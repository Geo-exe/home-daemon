namespace HomeDaemon.Api.Auth;

/// <summary>Outcome of the one-off administrator bootstrap command.</summary>
public sealed record AdminBootstrapResult(bool Created, Guid? UserId, string? Email)
{
    /// <summary>Returned when an administrator already exists; the command then does nothing.</summary>
    public static AdminBootstrapResult AlreadyInitialised { get; } = new(false, null, null);
}
