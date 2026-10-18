namespace HomeDaemon.Application.Common;

/// <summary>
/// Raised when a command cannot be completed because it conflicts with the current state,
/// for example deleting a room that still has chores. It is mapped to a 409 response.
/// </summary>
public sealed class EntityConflictException : Exception
{
    public EntityConflictException()
    {
    }

    public EntityConflictException(string message)
        : base(message)
    {
    }

    public EntityConflictException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
